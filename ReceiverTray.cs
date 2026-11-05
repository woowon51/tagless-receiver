using tagless_receiver.Models;
using tagless_receiver.Services;
namespace tagless_receiver;
public partial class ReceiverTray : Form
{
    private readonly ReceiverConfig config;
    private readonly BleScanService bleScanService=new();
    private readonly BleAdvertiseService bleAdvertiseService=new();
    public ReceiverTray()
    {
        Logger.Trace("cs_receiver_tray", "ReceiverTray", "ReceiverTray", "IN", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        InitializeComponent(); config=Program.Config;
        Logger.Trace("cs_receiver_tray", "ReceiverTray", "ReceiverTray", "CONFIG READY", "", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        ShowInTaskbar=false;WindowState=FormWindowState.Minimized;
        _=Handle; _=MonitorAsync();
        Logger.Trace("cs_receiver_tray", "ReceiverTray", "ReceiverTray", "MONITOR START", "", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
    }
    protected override void SetVisibleCore(bool value)=>base.SetVisibleCore(false);
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        Logger.Trace("cs_receiver_tray", "ReceiverTray", "OnFormClosing", "IN", "", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        bleScanService.Stop();bleAdvertiseService.Stop();
        Logger.Trace("cs_receiver_tray", "ReceiverTray", "OnFormClosing", "BLE STOPPED", "", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        base.OnFormClosing(e);
        Logger.Trace("cs_receiver_tray", "ReceiverTray", "OnFormClosing", "OUT", "", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
    }
    private async Task MonitorAsync()
    {
        Logger.Trace("cs_receiver_tray", "ReceiverTray", "MonitorAsync", "IN", "", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        bool running=false;
        bool firstRegisterCheck=true;
        while(!IsDisposed && !Disposing)
        {
            if(firstRegisterCheck)
            {
                Logger.Trace("cs_receiver_tray", "ReceiverTray", "MonitorAsync", "REGISTER CHECK START", "", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
                firstRegisterCheck=false;
            }
            var latest=await RegistrationService.RegisterCheckAsync(config.receiver_device_id,config.hardware_fingerprint);
            if(IsDisposed || Disposing)
            {
                Logger.Trace("cs_receiver_tray", "ReceiverTray", "MonitorAsync", "OUT DISPOSED", "", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
                return;
            }
            if(latest is null || !latest.registered)
            {if(running) {bleScanService.Stop();bleAdvertiseService.Stop();running=false;
                Logger.Trace("cs_receiver_tray", "ReceiverTray", "MonitorAsync", "REGISTERED → UNREGISTERED", "", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
            }}
            else
            {
                bool configChanged =
                    config.business_id != latest.business_id ||
                    config.class_id != latest.class_id ||
                    config.registered != latest.registered ||
                    config.config_version != latest.config_version;
                config.business_id=latest.business_id;config.class_id=latest.class_id;
                config.registered=true;config.config_version=latest.config_version;
                if(configChanged)
                {
                    ConfigService.Save(config);
                    Logger.Trace("cs_receiver_tray", "ReceiverTray", "MonitorAsync", "CONFIG CHANGED", "", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
                }
                if(!running){bleScanService.Start();bleAdvertiseService.Start();running=true;
                    Logger.Trace("cs_receiver_tray", "ReceiverTray", "MonitorAsync", "UNREGISTERED → REGISTERED", "", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
                }
            }
            await Task.Delay(5000);
        }
        Logger.Trace("cs_receiver_tray", "ReceiverTray", "MonitorAsync", "OUT", "", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
    }
}
