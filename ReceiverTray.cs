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
        InitializeComponent(); config=Program.Config;ShowInTaskbar=false;WindowState=FormWindowState.Minimized;
        _=Handle; _=MonitorAsync();
    }
    protected override void SetVisibleCore(bool value)=>base.SetVisibleCore(false);
    protected override void OnFormClosing(FormClosingEventArgs e)
    {bleScanService.Stop();bleAdvertiseService.Stop();base.OnFormClosing(e);}
    private async Task MonitorAsync()
    {
        bool running=false;
        while(!IsDisposed && !Disposing)
        {
            var latest=await RegistrationService.RegisterCheckAsync(config.receiver_device_id,config.hardware_fingerprint);
            if(IsDisposed || Disposing) return;
            if(latest is null || !latest.registered)
            {if(running) {bleScanService.Stop();bleAdvertiseService.Stop();running=false;}}
            else
            {
                config.business_id=latest.business_id;config.class_id=latest.class_id;
                config.registered=true;config.config_version=latest.config_version;
                ConfigService.Save(config);
                if(!running){bleScanService.Start();bleAdvertiseService.Start();running=true;}
            }
            await Task.Delay(5000);
        }
    }
}
