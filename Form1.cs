using tagless_receiver.Models;
using tagless_receiver.Services;

namespace tagless_receiver;

public partial class Form1 : Form
{
    private readonly ReceiverConfig config;

    public Form1()
    {
        InitializeComponent();

        Text = "Tagless Receiver";

        config = ConfigService.LoadOrCreate();

        // TODO : 화면에 표시
    }
}