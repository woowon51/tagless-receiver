using tagless_receiver.Models;
using tagless_receiver.Services;
namespace tagless_receiver;
static class Program
{
    public static ReceiverConfig Config { get; private set; } = null!;
    private static Mutex? operatorMutex;
    private const string MutexName=@"Global\MeHereReceiverOperatorV2";
    private const string StopName=@"Global\MeHereReceiverStopV2";
    public static void StopPreviousInstance()
    {
        Logger.Trace("cs_program", "Program", "StopPreviousInstance", "IN", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        if(EventWaitHandle.TryOpenExisting(StopName,out var signal)) {using(signal) {if(signal.Set()) { Logger.Trace("cs_program", "Program", "StopPreviousInstance", "STOP_SIGNAL SENT", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null); }}}
        operatorMutex=new Mutex(false,MutexName);
        try
        {
            if(!operatorMutex.WaitOne(TimeSpan.FromSeconds(15)))
            {
                Logger.Trace("cs_program", "Program", "StopPreviousInstance", "MUTEX TIMEOUT", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
                throw new InvalidOperationException("기존 수신기를 종료한 후 다시 설치해주세요.");
            }
            Logger.Trace("cs_program", "Program", "StopPreviousInstance", "MUTEX ACQUIRED", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        }
        catch(AbandonedMutexException) { Logger.Trace("cs_program", "Program", "StopPreviousInstance", "MUTEX ABANDONED", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null); }
    }
    [STAThread]
    static void Main(string[] args)
    {
        Logger.Trace("cs_program", "Program", "Main", "IN", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        ApplicationConfiguration.Initialize();
        WebSessionRecovery.RegistryRegister();
        try
        {
            if(args.Length==1 && args[0].StartsWith("taglessreceiver://session-recovery",StringComparison.OrdinalIgnoreCase))
            {
                Logger.Trace("cs_program", "Program", "Main", "SESSION_RECOVERY IN", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
                WebSessionRecovery.Delivery();
                Logger.Trace("cs_program", "Program", "Main", "SESSION_RECOVERY OUT", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
                return;
            }
            // Explicit reinstall intent carries no credentials; argument-free first install uses Config.
            bool installing=args.Length==1 && args[0]=="--install";
            if(installing) StopPreviousInstance();
            else
            {
                operatorMutex=new Mutex(false,MutexName);
                try {if(!operatorMutex.WaitOne(0)) return;} catch(AbandonedMutexException) {}
            }
            Logger.Trace("cs_program", "Program", "Main", "NORMAL_START IN", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
            Config=ConfigService.LoadOrCreate();
            Logger.Trace("cs_program", "Program", "Main", "NORMAL_START OUT", "", businessId: Config.business_id, classId: Config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: Config.receiver_device_id);
            if(installing || !Config.registered)
            {
                if(!installing)
                {
                    Logger.Trace("cs_program", "Program", "Main", "UNREGISTERED RECOVERY", "", businessId: Config.business_id, classId: Config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: Config.receiver_device_id);
                    WebSessionRecovery.Delivery();
                }
                Logger.Trace("cs_program", "Program", "Main", "INSTALL_REQUEST IN", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
                Config=AdditionalInstallDelivery.RunAsync().GetAwaiter().GetResult();
                Logger.Trace("cs_program", "Program", "Main", "INSTALL_REQUEST OUT", "", businessId: Config.business_id, classId: Config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: Config.receiver_device_id);
            }
            Logger.Trace("cs_program", "Program", "Main", "CONFIG_READY", "", businessId: Config.business_id, classId: Config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: Config.receiver_device_id);
            using var stop=new EventWaitHandle(false,EventResetMode.ManualReset,StopName);stop.Reset();
            using var tray=new ReceiverTray();
            _=Task.Run(()=>{stop.WaitOne();if(!tray.IsDisposed) tray.BeginInvoke(new Action(tray.Close));});
            Logger.Trace("cs_program", "Program", "Main", "TRAY_RUN IN", "", businessId: Config?.business_id, classId: Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: Config?.receiver_device_id);
            Application.Run(tray);
            Logger.Trace("cs_program", "Program", "Main", "TRAY_RUN OUT", "", businessId: Config?.business_id, classId: Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: Config?.receiver_device_id);
        }
        catch(Exception ex) { Logger.Trace("cs_program", "Program", "Main", "ERROR", $"type={ex.GetType().Name}; 수신기 초기화 또는 실행 처리 실패", businessId: Config?.business_id, classId: Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: Config?.receiver_device_id); MessageBox.Show(ex.Message,"MeHere 수신기 설치",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        finally {operatorMutex?.Dispose(); Logger.Trace("cs_program", "Program", "Main", "OUT", "", businessId: Config?.business_id, classId: Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: Config?.receiver_device_id); }
    }
}
