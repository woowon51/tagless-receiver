using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;
namespace tagless_receiver.Services;
public static class InstallIdentity
{
    private const string RegistryPath = @"Software\MeHere\Receiver";
    public static string Key()
    {
        using var r = Registry.CurrentUser.CreateSubKey(RegistryPath);
        if (r.GetValue("InstallKey") is byte[] data)
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser));
        string key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        r.SetValue("InstallKey", ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser), RegistryValueKind.Binary);
        return key;
    }
    public static string PendingRequest
    {
        get { using var r = Registry.CurrentUser.CreateSubKey(RegistryPath); return r.GetValue("PendingRequest") as string ?? ""; }
        set { using var r = Registry.CurrentUser.CreateSubKey(RegistryPath); r.SetValue("PendingRequest", value); }
    }
}
