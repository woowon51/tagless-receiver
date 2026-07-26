using tagless_receiver.Models;
using tagless_receiver.Services;

namespace tagless_receiver;


public partial class ReceiverTray : Form
{

    private ReceiverConfig config;

    private readonly BleScanService bleScanService = new();

    public ReceiverTray()
    {
        InitializeComponent();

        config = Program.Config;

        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;

        _ = InitializeReceiverAsync();

        Logger.Write("========== Receiver Tray 시작 ==================");
    }

    protected override void SetVisibleCore(bool value)
    {
        base.SetVisibleCore(false);
    }


    private async Task InitializeReceiverAsync()
    {
        Logger.Write(
            $"[Receiver] 시작 registered={config.registered}"
        );

        if (config.registered)
        {
            Logger.Write(
                "[Receiver] 이미 등록 완료"
            );

            // TODO: BLE Scan 시작
            bleScanService.Start();
            return;
        }

        ReceiverConfig? serverConfig =
            await RegistrationService.GetRegistrationAsync(
                config.receiver_device_id
            );

        if (serverConfig is null)
        {
            Logger.Write(
                "[Receiver] 미등록 장치 - 등록 페이지 열기"  
            );

            /*            // 등록페이지를 브라우저로 열어야 한다.
            C# → /receiver/qr?receiver_device_id=UUID
            QR 화면 → /receiver/start?...&receiver_device_id=UUID
            screen1 → screen2 hidden
            screen2 → install_welcome()
            DB 저장
            */

            string registrationUrl =
                //        $"{ApiConfig.BaseUrl}/receiver/qr" +
                "https://tagless-api-production.up.railway.app/receiver/qr" +
                $"?receiver_device_id={Uri.EscapeDataString(config.receiver_device_id)}";

            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = registrationUrl,
                    UseShellExecute = true
                }
            );

            return;
        }

        Logger.Write(
            $"[Receiver] 서버 응답: " +
            $"business_id={serverConfig.business_id}, " +
            $"class_id={serverConfig.class_id}, " +
            $"registered={serverConfig.registered}"
        );

        /*
        if (!serverConfig.registered)
        {
            System.Diagnostics.Debug.WriteLine(
                "[Receiver] 서버상 미등록 상태"
            );

            return;
        }
        */

        config.business_id = serverConfig.business_id;
        config.class_id = serverConfig.class_id;
        config.registered = true;
        config.config_version = serverConfig.config_version;


        ConfigService.Save(config);

        Logger.Write(
            @"[Receiver] C:\TaglessReceiver\config.json 저장 완료"
        );

        // TODO: BLE Scan 시작
        bleScanService.Start();
    }
}