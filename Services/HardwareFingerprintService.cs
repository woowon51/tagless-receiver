using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;

namespace tagless_receiver.Services;

public static class HardwareFingerprintService
{
    public static string Create()
    {
        Logger.Write("[HardwareFingerprint] 생성 시작");
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
            throw new InvalidOperationException("Windows PC 식별정보를 읽을 수 없습니다.");
        }



        using SHA256 sha = SHA256.Create();

        byte[] hash =
            sha.ComputeHash(
                Encoding.UTF8.GetBytes(machineGuid)
            );
        string fingerprint = Convert.ToHexString(hash);


        Logger.Write("[HardwareFingerprint] 생성 완료");

        return Convert.ToHexString(hash);
    }
}
