using tagless_receiver.Models;
using tagless_receiver.Services;

namespace tagless_receiver;

public partial class ReceiverTray : Form
{
    private readonly ReceiverConfig config;

    public ReceiverTray()
    {
        InitializeComponent();

        Text = "Tagless Receiver";

        config = ConfigService.LoadOrCreate();

        // TODO : 화면에 표시
    }
}