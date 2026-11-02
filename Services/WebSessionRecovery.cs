using System.Diagnostics;
using Microsoft.Win32;
namespace tagless_receiver.Services;
public static class WebSessionRecovery
{
    public static void RegistryRegister()
    {
        Logger.Trace("cs_web_recovery", "WebSessionRecovery", "RegistryRegister", "IN", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        string? exe=Environment.ProcessPath;
        if(string.IsNullOrWhiteSpace(exe))
        {
            Logger.Trace("cs_web_recovery", "WebSessionRecovery", "RegistryRegister", "EXE MISSING", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
            return;
        }
        using var key=Registry.CurrentUser.CreateSubKey(@"Software\Classes\taglessreceiver");
        key.SetValue("","URL:MeHere Receiver Protocol");key.SetValue("URL Protocol","");
        using var command=key.CreateSubKey(@"shell\open\command");command.SetValue("",$"\"{exe}\" \"%1\"");
        Logger.Trace("cs_web_recovery", "WebSessionRecovery", "RegistryRegister", "REGISTRY REGISTERED", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        Logger.Trace("cs_web_recovery", "WebSessionRecovery", "RegistryRegister", "OUT", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
    }
    public static void Delivery()
    {
        Logger.Trace("cs_web_recovery", "WebSessionRecovery", "Delivery", "IN", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        // Reuse request-bound local proof. A public receiver ID is never a login credential.
        var process = Process.Start(new ProcessStartInfo("https://taglesscheck.com/receiver/addinstall"){UseShellExecute=true});
        if (process != null)
        {
            Logger.Trace("cs_web_recovery", "WebSessionRecovery", "Delivery", "BROWSER OPEN", "브라우저 실행 요청 반환; 페이지 로딩 확인 아님", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        }
        Logger.Trace("cs_web_recovery", "WebSessionRecovery", "Delivery", "OUT", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
    }
}
