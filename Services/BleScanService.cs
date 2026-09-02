using System.Diagnostics;
using Windows.Devices.Bluetooth.Advertisement;
using System.Net.Http.Json;

namespace tagless_receiver.Services;

public sealed class BleScanService
{
    private BluetoothLEAdvertisementWatcher? watcher;

    private static readonly HttpClient httpClient = new();

    private const string BleLinkApiUrl =
        "https://tagless-api-production.up.railway.app/receiver/ble-link";


    // 같은 Sender를 BLE 광고마다 서버로 보내지 않기 위한 제한
    private readonly Dictionary<int, DateTime> lastBleLinkReport = new();

    private readonly object reportLock = new();

    private static readonly TimeSpan BleLinkReportInterval =
        TimeSpan.FromSeconds(10);

    private long bleDebugCount = 0;
    private long taglessCount = 0;

    private static readonly Guid TaglessSenderServiceUuid =
        Guid.Parse(
            "e747f937-5029-55a4-90ed-370194b05a34"
        );

    private readonly AttendanceChecker attendanceChecker = new();

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

        lock (reportLock)
        {
            lastBleLinkReport.Clear();
        }

        Logger.Write("[BLE] Scan 중지");
    }

    private void OnAdvertisementReceived(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementReceivedEventArgs args)
    {
        bleDebugCount++;

        if (bleDebugCount == 1 || bleDebugCount % 5000 == 0)
        {
            Logger.Write(
                $"[BLE-DEBUG] 일반 BLE 광고 수신 {bleDebugCount}회"
            );
        }

        foreach (var section in args.Advertisement.DataSections)
        {

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

            taglessCount++;

            if (taglessCount == 1 || taglessCount % 500 == 0)
            {
                Logger.Write(
                    $"[BLE] Tagless Sender 확인 {taglessCount}회: " +
                    $"sender_device_id={senderDeviceId}, " +
                    $"address={bluetoothAddress}, " +
                    $"RSSI={rssi}"
                );
            }

            // 출석 START / END 판정용
            attendanceChecker.Seen(senderDeviceId);

            // 기존 ble_link 서버 보고
            if (ShouldReportBleLink(senderDeviceId))
            {
                _ = ReportBleLinkAsync(
                    senderDeviceId,
                    rssi
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

    private bool ShouldReportBleLink(
        int senderDeviceId)
    {
        lock (reportLock)
        {
            DateTime now =
                DateTime.UtcNow;

            if (
                lastBleLinkReport.TryGetValue(
                    senderDeviceId,
                    out DateTime lastReport
                )
            )
            {
                if (
                    now - lastReport
                    < BleLinkReportInterval
                )
                {
                    return false;
                }
            }

            lastBleLinkReport[senderDeviceId] =
                now;

            return true;
        }
    }

    private async Task ReportBleLinkAsync(
        int senderDeviceId,
        short rssi)
    {
        try
        {
            var config =
                tagless_receiver.Program.Config;

            if (
                config == null ||
                string.IsNullOrWhiteSpace(
                    config.receiver_device_id
                )
            )
            {
                Logger.Write(
                    "[BLE-LINK] receiver_device_id 없음"
                );

                return;
            }


            var payload = new
            {
                receiver_device_id =
                    config.receiver_device_id,

                sender_device_id =
                    senderDeviceId,

                rssi =
                    (int)rssi
            };


            using HttpResponseMessage response =
                await httpClient.PostAsJsonAsync(
                    BleLinkApiUrl,
                    payload
                );


            string responseText =
                await response.Content.ReadAsStringAsync();


            if (!response.IsSuccessStatusCode)
            {
                Logger.Write(
                    "[BLE-LINK] 서버 전송 실패: " +
                    $"status={(int)response.StatusCode}, " +
                    $"body={responseText}"
                );

                return;
            }

            /*
            Logger.Write(
                "[BLE-LINK] 수신 확인 서버 전송: " +
                $"sender_device_id={senderDeviceId}, " +
                $"RSSI={rssi}"
            );
            */
        }
        catch (Exception ex)
        {
            Logger.Error(
                "[BLE-LINK] ReportBleLinkAsync 실패",
                ex
            );
        }
    }

}