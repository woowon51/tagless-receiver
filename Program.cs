using tagless_receiver.Models;
using tagless_receiver.Services;

namespace tagless_receiver;

static class Program
{
    public static ReceiverConfig Config { get; private set; } = null!;

    /*
    평상시 TRE 실행
    → RegistryRegister()
    → taglessreceiver:// 등록
    → 기존 ReceiverTray 실행

    웹의 [사용자정보 복구 시작] 클릭
    → Windows:
       taglessreceiver://session-recovery
    → 새 TRE 프로세스 기동
    → Program.cs가 URL 인자 발견
    → Delivery()
    → 기존 config.json 읽기
    → receiver_device_id 확보
    → taglesscheck.com/receiver/config_json_has?... 오픈
    → C# 복구 프로세스 종료
    */

    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        WebSessionRecovery.RegistryRegister();

        if (
            args.Length > 0 &&
            args[0].StartsWith(
                "taglessreceiver://session-recovery",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            WebSessionRecovery.Delivery();
            return;
        }

        Config = ConfigService.LoadOrCreate();

        Application.Run(new ReceiverTray());
    }
}