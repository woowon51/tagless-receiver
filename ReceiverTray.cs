using tagless_receiver.Models;
using tagless_receiver.Services;

namespace tagless_receiver;

public partial class ReceiverTray : Form
{
    private ReceiverConfig config;

    public ReceiverTray()
    {
        InitializeComponent();

        config = Program.Config;

        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;

        _ = InitializeReceiverAsync();
    }

    protected override void SetVisibleCore(bool value)
    {
        base.SetVisibleCore(false);
    }


    private async Task InitializeReceiverAsync()
    {
        System.Diagnostics.Debug.WriteLine(
            $"[Receiver] 시작 registered={config.registered}"
        );

        if (config.registered)
        {
            System.Diagnostics.Debug.WriteLine(
                "[Receiver] 이미 등록 완료"
            );

            // TODO: BLE Scan 시작
            return;
        }

        ReceiverConfig? serverConfig =
            await RegistrationService.GetRegistrationAsync(
                config.receiver_device_id
            );

        if (serverConfig is null)
        {
            System.Diagnostics.Debug.WriteLine(
                "[Receiver] 서버 응답을 ReceiverConfig로 읽지 못함"
            );

            return;
        }

        System.Diagnostics.Debug.WriteLine(
            $"[Receiver] 서버 응답: " +
            $"business_id={serverConfig.business_id}, " +
            $"class_id={serverConfig.class_id}, " +
            $"registered={serverConfig.registered}"
        );

        if (!serverConfig.registered)
        {
            System.Diagnostics.Debug.WriteLine(
                "[Receiver] 서버상 미등록 상태"
            );

            return;
        }

        config.business_id = serverConfig.business_id;
        config.class_id = serverConfig.class_id;
        config.registered = true;
        config.config_version = serverConfig.config_version;


        ConfigService.Save(config);

        System.Diagnostics.Debug.WriteLine(
            @"[Receiver] C:\TaglessReceiver\config.json 저장 완료"
        );

        // TODO: BLE Scan 시작
    }
}