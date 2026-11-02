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
        TimeSpan.FromSeconds(2);

    private long bleDebugCount = 0;
    private long taglessCount = 0;

    private static readonly Guid TaglessSenderServiceUuid =
        Guid.Parse(
            "e747f937-5029-55a4-90ed-370194b05a34"
        );

    private readonly AttendanceChecker attendanceChecker = new();

    // Log-only deterministic abbreviation; never returns the receiver ID itself.
    private static string? GetReceiverLogId()
    {
        try
        {
            string? receiverId = Program.Config?.receiver_device_id;
            if (string.IsNullOrWhiteSpace(receiverId))
                return null;

            byte[] digest = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(receiverId));
            return "h" + Convert.ToHexString(digest.AsSpan(0, 8));
        }
        catch
        {
            // Identifier formatting must not affect BLE processing.
            return null;
        }
    }

    public void Start()
    {
        Logger.Trace("cs_ble_scan", "BleScanService", "Start", "IN", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
        if (watcher != null)
        {
            Logger.Trace("cs_ble_scan", "BleScanService", "Start", "SKIP ALREADY STARTED", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
            return;
        }

        watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };

        watcher.Received += OnAdvertisementReceived;
        watcher.Stopped += OnWatcherStopped;

        watcher.Start();

        Logger.Trace("cs_ble_scan", "BleScanService", "Start", "OUT SCAN STARTED", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
    }

    public void Stop()
    {
        Logger.Trace("cs_ble_scan", "BleScanService", "Stop", "IN", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
        if (watcher == null)
        {
            Logger.Trace("cs_ble_scan", "BleScanService", "Stop", "SKIP NOT STARTED", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
            return;
        }

        watcher.Stop();
        Logger.Trace("cs_ble_scan", "BleScanService", "Stop", "SCAN STOPPED", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
        watcher.Received -= OnAdvertisementReceived;
        watcher.Stopped -= OnWatcherStopped;
        watcher = null;

        lock (reportLock)
        {
            lastBleLinkReport.Clear();
        }

        Logger.Trace("cs_ble_scan", "BleScanService", "Stop", "OUT", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
    }

    private void OnAdvertisementReceived(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementReceivedEventArgs args)
    {
        bleDebugCount++;

        foreach (var uuid in args.Advertisement.ServiceUuids)
        {
            if (!uuid.ToString().StartsWith(
                "00000036",
                StringComparison.OrdinalIgnoreCase))
                continue;

            Logger.Trace("cs_ble_scan", "BleScanService", "OnAdvertisementReceived", "BLE-IOS-TEST", $"UUID={uuid}, RSSI={args.RawSignalStrengthInDBm}", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
        }

        if (bleDebugCount == 1 || bleDebugCount % 5000 == 0)
        {
            Logger.Trace("cs_ble_scan", "BleScanService", "OnAdvertisementReceived", "IN", $"BLE-DEBUG 일반 BLE 광고 수신 {bleDebugCount}회", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
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
                Logger.Trace("cs_ble_scan", "BleScanService", "OnAdvertisementReceived", "IN", $"MHSender 확인 {taglessCount}회: addr={bluetoothAddress}, RSSI={rssi}", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: senderDeviceId, receiverDeviceId: GetReceiverLogId());
            }

            // 출석 START / END 판정용
            attendanceChecker.Seen(senderDeviceId);

            // 기존 ble_link 서버 보고
            if (ShouldReportBleLink(senderDeviceId))
            {
                _ = ReportBleLinkAsync(
                    senderDeviceId,
                    rssi,
                    args.Timestamp.UtcDateTime
                );
            }

            return;
        }
    }

    private void OnWatcherStopped(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        Logger.Trace("cs_ble_scan", "BleScanService", "OnWatcherStopped", args.Error == Windows.Devices.Bluetooth.BluetoothError.Success ? "STOPPED" : "ERROR WATCHER STOPPED", $"error={args.Error}", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: GetReceiverLogId());
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

            Logger.Trace("cs_ble_scan", "BleScanService", "ShouldReportBleLink", "REPORT", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: senderDeviceId, receiverDeviceId: GetReceiverLogId());
            return true;
        }
    }

    private async Task ReportBleLinkAsync(
        int senderDeviceId,
        short rssi,
        DateTime observedAt)
    {
        Logger.Trace("cs_ble_scan", "BleScanService", "ReportBleLinkAsync", "IN", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: senderDeviceId, receiverDeviceId: GetReceiverLogId());
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
                Logger.Trace("cs_ble_scan", "BleScanService", "ReportBleLinkAsync", "CONFIG MISSING", "", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: senderDeviceId, receiverDeviceId: GetReceiverLogId());

                return;
            }


            var payload = new
            {
                receiver_device_id =
                    config.receiver_device_id,

                sender_device_id =
                    senderDeviceId,

                rssi =
                    (int)rssi,
                observed_at = observedAt
            };


            using HttpResponseMessage response =
                await httpClient.PostAsJsonAsync(
                    BleLinkApiUrl,
                    payload
                );


            Logger.Trace("cs_ble_scan", "BleScanService", "ReportBleLinkAsync", $"HTTP({(int)response.StatusCode})", response.IsSuccessStatusCode ? "서버 응답 수신" : "서버가 BLE 링크 보고를 거부하거나 처리하지 못함", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: senderDeviceId, receiverDeviceId: GetReceiverLogId());

            string responseText =
                await response.Content.ReadAsStringAsync();


            if (!response.IsSuccessStatusCode)
            {
                // The status was logged on receipt; never log the response body.

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
            Logger.Trace("cs_ble_scan", "BleScanService", "ReportBleLinkAsync", "ERROR", $"type={ex.GetType().Name}; BLE 링크 보고 처리 실패", businessId: Program.Config?.business_id, classId: Program.Config?.class_id, businessMemberId: null, staffId: null, senderDeviceId: senderDeviceId, receiverDeviceId: GetReceiverLogId());
        }
    }

}