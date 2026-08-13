using System.Diagnostics;
using Windows.Devices.Bluetooth.Advertisement;

namespace tagless_receiver.Services;

public sealed class BleScanService
{
    private BluetoothLEAdvertisementWatcher? watcher;

    private readonly HashSet<int> detectedSenderIds = new();

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

        detectedSenderIds.Clear();

        Logger.Write("[BLE] Scan 중지");
    }

    private void OnAdvertisementReceived(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementReceivedEventArgs args)
    {
        Logger.Write(
            $"[BLE-DEBUG] 광고 수신 " +
            $"address={args.BluetoothAddress:X12}, " +
            $"RSSI={args.RawSignalStrengthInDBm}, " +
            $"sections={args.Advertisement.DataSections.Count}"
        );

        foreach (var section in args.Advertisement.DataSections)
        {
            Logger.Write(
                $"[BLE-DEBUG] section " +
                $"type=0x{section.DataType:X2}, " +
                $"length={section.Data.Length}"
            );
            // 128-bit Service Data
            if (section.DataType != 0x21)
                continue;

            var reader =
                Windows.Storage.Streams.DataReader.FromBuffer(section.Data);

            byte[] bytes = new byte[section.Data.Length];
            reader.ReadBytes(bytes);

            // UUID 16바이트 + sender_device_id 4바이트
            if (bytes.Length != 20)
                continue;

            Logger.Write(
                $"[BLE-DEBUG] ServiceData=" +
                BitConverter.ToString(bytes)
            );


            // BLE Service Data에 들어오는 Tagless UUID의 실제 바이트 순서
            byte[] taglessUuidBytes =
            {
            0x34, 0x5A, 0xB0, 0x94,
            0x01, 0x37, 0xED, 0x90,
            0xA4, 0x55, 0x29, 0x50,
            0x37, 0xF9, 0x47, 0xE7
        };

            bool uuidMatch = true;

            for (int i = 0; i < 16; i++)
            {
                if (bytes[i] != taglessUuidBytes[i])
                {
                    uuidMatch = false;
                    break;
                }
            }

            if (!uuidMatch)
                continue;

            // 뒤 4바이트 = sender_device_id
            int senderDeviceId =
                BitConverter.ToInt32(bytes, 16);

            short rssi =
                args.RawSignalStrengthInDBm;

            string bluetoothAddress =
                args.BluetoothAddress.ToString("X12");

            // 같은 Sender는 최초 1회만 로그
            if (detectedSenderIds.Add(senderDeviceId))
            {
                Logger.Write(
                    $"[BLE] Tagless Sender 확인: " +
                    $"sender_device_id={senderDeviceId}, " +
                    $"address={bluetoothAddress}, " +
                    $"RSSI={rssi}"
                );
            }

            bool isFirst = detectedSenderIds.Add(senderDeviceId);

            Logger.Write(
                $"[BLE-DEBUG] Tagless UUID 일치: " +
                $"sender_device_id={senderDeviceId}, " +
                $"first={isFirst}"
            );

            if (isFirst)
            {
                Logger.Write(
                    $"[BLE] Tagless Sender 확인: " +
                    $"sender_device_id={senderDeviceId}, " +
                    $"address={bluetoothAddress}, " +
                    $"RSSI={rssi}"
                );
            }

            return;
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