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
            Logger.Write("[HardwareFingerprint] MachineGuid 읽기 실패");
        }

        Logger.Write($"[HardwareFingerprint] MachineGuid = {machineGuid}");

        using SHA256 sha = SHA256.Create();

        byte[] hash =
            sha.ComputeHash(
                Encoding.UTF8.GetBytes(machineGuid)
            );
        string fingerprint = Convert.ToHexString(hash);
        Logger.Write($"[HardwareFingerprint] Fingerprint = {fingerprint}");

        Logger.Write("[HardwareFingerprint] 생성 완료");

        return Convert.ToHexString(hash);
    }
}