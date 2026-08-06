using System.Diagnostics;
using Windows.Devices.Bluetooth.Advertisement;

namespace tagless_receiver.Services;

public sealed class BleScanService
{
    private BluetoothLEAdvertisementWatcher? watcher;

    private bool taglessSenderDetectedLogged = false;

    private static readonly Guid TaglessSenderServiceUuid =
        Guid.Parse(
            "e747f937-5029-55a4-90ed-370194b05a34"
        );

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

        Logger.Write("[BLE] Scan 시작");
    }

    public void Stop()
    {
        if (watcher == null)
            return;

        watcher.Stop();
        watcher.Received -= OnAdvertisementReceived;
        watcher.Stopped -= OnWatcherStopped;
        watcher = null;

        taglessSenderDetectedLogged = false;

        Logger.Write("[BLE] Scan 중지");
    }

    private void OnAdvertisementReceived(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementReceivedEventArgs args)
    {
        Logger.Write(
            $"[BLE] Advertisement 수신  UUID={args.Advertisement.ServiceUuids.Count}"
        );

        Logger.Write(
            $"[BLE] UUID Count = {args.Advertisement.ServiceUuids.Count}"
        );

        foreach (var uuid in args.Advertisement.ServiceUuids)
        {
            Logger.Write($"UUID={uuid}");
        }

        bool isTaglessSender =
            args.Advertisement.ServiceUuids.Contains(
                TaglessSenderServiceUuid
            );

        if (!isTaglessSender)
            return;

        string bluetoothAddress =
            args.BluetoothAddress.ToString("X12");

        short rssi =
            args.RawSignalStrengthInDBm;

        string localName =
            args.Advertisement.LocalName ?? "";

        if (!taglessSenderDetectedLogged)
        {
            taglessSenderDetectedLogged = true;

            Logger.Write(
                $"[BLE] Tagless Sender 감지: " +
                $"service_uuid={TaglessSenderServiceUuid}, " +
                $"address={bluetoothAddress}, " +
                $"RSSI={rssi}, " +
                $"name={localName}"
            );
        }
    }

    private void OnWatcherStopped(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        Logger.Write(
            $"[BLE] Watcher 중지 error={args.Error}"
        );
    }
}