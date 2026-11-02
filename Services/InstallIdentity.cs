using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;
namespace tagless_receiver.Services;
public static class InstallIdentity
{
    private const string RegistryPath = @"Software\MeHere\Receiver";
    private static int pendingCleanupErrorLogged;
    public static string Key()
    {
        Logger.Trace("cs_install_identity", "InstallIdentity", "Key", "IN", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        using var r = Registry.CurrentUser.CreateSubKey(RegistryPath);
        try
        {
            bool hadPendingRequest = r.GetValue("PendingRequest") is not null;
            r.DeleteValue("PendingRequest", false);
            System.Threading.Interlocked.Exchange(ref pendingCleanupErrorLogged, 0);
            if (hadPendingRequest)
            {
                Logger.Trace("cs_install_identity", "InstallIdentity", "Key", "OK PENDING_REQUEST_CLEARED", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
            }
        }
        catch (Exception ex)
        {
            if (System.Threading.Interlocked.Exchange(ref pendingCleanupErrorLogged, 1) == 0)
            {
                Logger.Trace("cs_install_identity", "InstallIdentity", "Key", "ERR", $"type={ex.GetType().Name}; 과거 PendingRequest 정리 실패", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
            }
        }
        if (r.GetValue("InstallKey") is byte[] data)
        {
            string loadedKey = Encoding.UTF8.GetString(ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser));
            Logger.Trace("cs_install_identity", "InstallIdentity", "Key", "KEY LOADED", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
            Logger.Trace("cs_install_identity", "InstallIdentity", "Key", "OUT", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
            return loadedKey;
        }
        string key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        r.SetValue("InstallKey", ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser), RegistryValueKind.Binary);
        Logger.Trace("cs_install_identity", "InstallIdentity", "Key", "KEY CREATED", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        Logger.Trace("cs_install_identity", "InstallIdentity", "Key", "OUT", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        return key;
    }
}
