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
        if(EventWaitHandle.TryOpenExisting(StopName,out var signal)) {using(signal) signal.Set();}
        operatorMutex=new Mutex(false,MutexName);
        try {if(!operatorMutex.WaitOne(TimeSpan.FromSeconds(15))) throw new InvalidOperationException("기존 수신기를 종료한 후 다시 설치해주세요.");}
        catch(AbandonedMutexException) {}
    }
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        WebSessionRecovery.RegistryRegister();
        try
        {
            if(args.Length==1 && args[0].StartsWith("taglessreceiver://session-recovery",StringComparison.OrdinalIgnoreCase))
            {WebSessionRecovery.Delivery();return;}
            bool installing=args.Length==2 && args[0]=="--install-request";
            if(installing || !string.IsNullOrEmpty(InstallIdentity.PendingRequest)) StopPreviousInstance();
            else
            {
                operatorMutex=new Mutex(false,MutexName);
                try {if(!operatorMutex.WaitOne(0)) return;} catch(AbandonedMutexException) {}
            }
            if(installing) Config=AdditionalInstallDelivery.RunAsync(args[1]).GetAwaiter().GetResult();
            else if(!string.IsNullOrEmpty(InstallIdentity.PendingRequest)) Config=AdditionalInstallDelivery.ResumeAsync(InstallIdentity.PendingRequest).GetAwaiter().GetResult();
            else
            {
                Config=ConfigService.LoadOrCreate();
                if(!Config.registered) {WebSessionRecovery.Delivery();return;}
            }
            using var stop=new EventWaitHandle(false,EventResetMode.ManualReset,StopName);stop.Reset();
            using var tray=new ReceiverTray();
            _=Task.Run(()=>{stop.WaitOne();if(!tray.IsDisposed) tray.BeginInvoke(new Action(tray.Close));});
            Application.Run(tray);
        }
        catch(Exception ex) {MessageBox.Show(ex.Message,"MeHere 수신기 설치",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        finally {operatorMutex?.Dispose();}
    }
}
