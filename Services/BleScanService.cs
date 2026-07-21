using System.Diagnostics;
using Windows.Devices.Bluetooth.Advertisement;

namespace tagless_receiver.Services;

public sealed class BleScanService
{
    private BluetoothLEAdvertisementWatcher? watcher;

    public void Start()
    {
        if (watcher != null)
            return;

        watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };

        watcher.Received += OnAdvertisementReceived;
        watcher.Stopped += OnWatcherStopped;

        watcher.Start();

        Debug.WriteLine("[BLE] Scan 시작");
    }

    public void Stop()
    {
        if (watcher == null)
            return;

        watcher.Stop();
        watcher.Received -= OnAdvertisementReceived;
        watcher.Stopped -= OnWatcherStopped;
        watcher = null;

        Debug.WriteLine("[BLE] Scan 중지");
    }

    private void OnAdvertisementReceived(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementReceivedEventArgs args)
    {
        string bluetoothAddress =
            args.BluetoothAddress.ToString("X12");

        short rssi = args.RawSignalStrengthInDBm;

        string localName =
            args.Advertisement.LocalName ?? "";

        Debug.WriteLine(
            $"[BLE] address={bluetoothAddress}, " +
            $"rssi={rssi}, " +
            $"name={localName}"
        );
    }

    private void OnWatcherStopped(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        Debug.WriteLine(
            $"[BLE] Watcher 중지 error={args.Error}"
        );
    }
}