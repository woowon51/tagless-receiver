using System.Diagnostics;
using Microsoft.Win32;
namespace tagless_receiver.Services;
public static class WebSessionRecovery
{
    public static void RegistryRegister()
    {
        string? exe=Environment.ProcessPath;if(string.IsNullOrWhiteSpace(exe)) return;
        using var key=Registry.CurrentUser.CreateSubKey(@"Software\Classes\taglessreceiver");
        key.SetValue("","URL:MeHere Receiver Protocol");key.SetValue("URL Protocol","");
        using var command=key.CreateSubKey(@"shell\open\command");command.SetValue("",$"\"{exe}\" \"%1\"");
    }
    public static void Delivery()
    {
        // Reuse request-bound local proof. A public receiver ID is never a login credential.
        Process.Start(new ProcessStartInfo("https://taglesscheck.com/receiver/addinstall"){UseShellExecute=true});
    }
}
