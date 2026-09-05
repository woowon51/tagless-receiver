using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;
using tagless_receiver.Models;

namespace tagless_receiver.Services;

public static class WebSessionRecovery
{
    // taglessreceiver:// URL을 Tagless Receiver와 연결
    public static void RegistryRegister()
    {
        string? exePath = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(exePath))
            return;

        using RegistryKey key =
            Registry.CurrentUser.CreateSubKey(
                @"Software\Classes\taglessreceiver"
            );

        key.SetValue("", "URL:Tagless Receiver Protocol");
        key.SetValue("URL Protocol", "");

        using RegistryKey command =
            key.CreateSubKey(@"shell\open\command");

        command.SetValue(
            "",
            $"\"{exePath}\" \"%1\""
        );
    }


    // config.json의 receiver_device_id를 Web으로 전달
    public static void Delivery()
    {
        const string configPath =
            @"C:\TaglessReceiver\config.json";

        if (!File.Exists(configPath))
        {
            MessageBox.Show(
                "사용자정보를 확인할 수 없습니다.\n" +
                "인원점검 수신앱을 설치 또는 재설치 해주세요.\n\n" +
                "확인 버튼을 누르면 (재)설치 페이지가 열립니다.",
                "Tagless 인원점검",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );


            Process.Start(
                new ProcessStartInfo("https://taglesscheck.com/receiver/qr")
                {
                    UseShellExecute = true
                }
            );

            return;

        }

        ReceiverConfig? config =
            JsonSerializer.Deserialize<ReceiverConfig>(
                File.ReadAllText(configPath)
            );

        if (
            config == null ||
            string.IsNullOrWhiteSpace(config.receiver_device_id) ||
            !config.registered
        )
        {
            MessageBox.Show(
                "사용자정보를 확인할 수 없습니다.\n" +
                "인원점검 수신앱을 설치 또는 재설치 해주세요.\n\n" +
                "확인 버튼을 누르면 (재)설치 페이지가 열립니다.",
                "Tagless 인원점검",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            Process.Start(
                new ProcessStartInfo("https://taglesscheck.com/receiver/qr")
                {
                    UseShellExecute = true
                }
            );

            return;
        }

        Process.Start(
            new ProcessStartInfo(
                "https://taglesscheck.com/receiver/config_json_has" +
                "?receiver_device_id=" +
                Uri.EscapeDataString(config.receiver_device_id)
            )
            {
                UseShellExecute = true
            }
        );
    }
}