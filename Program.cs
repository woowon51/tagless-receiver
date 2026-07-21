using tagless_receiver.Models;
using tagless_receiver.Services;

namespace tagless_receiver;

static class Program
{
    public static ReceiverConfig Config { get; private set; } = null!;

    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Receiver 설정파일 읽기
        // config.json이 없으면 자동 생성
        Config = ConfigService.LoadOrCreate();

        Application.Run(new ReceiverTray());
    }
}