using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;

namespace tagless_receiver.Services;

public static class HardwareFingerprintService
{
    public static string Create()
    {
        Logger.Trace("cs_hardware_fingerprint", "HardwareFingerprintService", "Create", "IN", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        string machineGuid = "";

        using RegistryKey? key =
            Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Cryptography"
            );

        if (key != null)
        {
            machineGuid =
                key.GetValue("MachineGuid")?.ToString() ?? "";
        }

        if (string.IsNullOrWhiteSpace(machineGuid))
        {
            Logger.Trace("cs_hardware_fingerprint", "HardwareFingerprintService", "Create", "ERROR MACHINE GUID MISSING", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
            throw new InvalidOperationException("Windows PC 식별정보를 읽을 수 없습니다.");
        }



        using SHA256 sha = SHA256.Create();

        byte[] hash =
            sha.ComputeHash(
                Encoding.UTF8.GetBytes(machineGuid)
            );
        string fingerprint = Convert.ToHexString(hash);


        Logger.Trace("cs_hardware_fingerprint", "HardwareFingerprintService", "Create", "FINGERPRINT CREATED", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);

        Logger.Trace("cs_hardware_fingerprint", "HardwareFingerprintService", "Create", "OUT", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        return Convert.ToHexString(hash);
    }
}
