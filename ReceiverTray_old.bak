using tagless_receiver.Models;
using tagless_receiver.Services;

namespace tagless_receiver;


public partial class ReceiverTray : Form
{

    private ReceiverConfig config;

    private readonly BleScanService bleScanService = new();
    private readonly BleAdvertiseService bleAdvertiseService = new();

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

        // 이미 등록된 장치
        if (config.registered)
        {
            Logger.Write(
                "[Receiver] 이미 등록 완료"
            );

            bleScanService.Start();       // 안드로이드용
            bleAdvertiseService.Start();  // 아이폰 ios 용
            return;
        }

        ReceiverConfig? serverConfig = null;

        bool pendingLogged = false;

        while (true)
        {
            serverConfig =
                await RegistrationService.RegisterCheckAsync(
                    config.receiver_device_id,
                    config.hardware_fingerprint
                );

            // 서버 통신 실패 또는 응답 해석 실패
            if (serverConfig is null)
            {
                Logger.Write(
                    "[Receiver] register-check 실패 - 2초 후 재조회"
                );

                await Task.Delay(2000);
                continue;
            }

            // 아직 정식 등록 전
            if (!serverConfig.registered)
            {
                if (!pendingLogged)
                {
                    config.business_id = null;
                    config.class_id = null;
                    config.registered = false;
                    config.config_version =
                        serverConfig.config_version;

                    ConfigService.Save(config);

                    Logger.Write(
                        "[Receiver] 임시등록 완료 - 등록상태 재조회 시작: " +
                        $"business_id={serverConfig.business_id}, " +
                        $"class_id={serverConfig.class_id}, " +
                        $"registered={serverConfig.registered}"
                    );

                    pendingLogged = true;
                }

                await Task.Delay(2000);
                continue;
            }

            // registered=true가 되었으므로 반복 종료
            break;
        }

        // 정식 등록 완료
        config.business_id = serverConfig.business_id;
        config.class_id = serverConfig.class_id;
        config.registered = true;
        config.config_version =
            serverConfig.config_version;

        Logger.Write(
            $"[Receiver] 정식등록 확인: " +
            $"business_id={serverConfig.business_id}, " +
            $"class_id={serverConfig.class_id}, " +
            $"registered={serverConfig.registered}"
        );

        ConfigService.Save(config);

        Logger.Write(
            @"[Receiver] C:\TaglessReceiver\config.json 저장 완료"
        );

        Logger.Write(
            "[Receiver] BLE 스캔 시작"
        );

        MessageBox.Show(
            "등록이 완료되어 BLE 스캔을 시작합니다.",
            "Tagless Receiver",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );

        bleScanService.Start();        // 안드로이드용
        bleAdvertiseService.Start();   // 아이폰 ios 용
    }
}