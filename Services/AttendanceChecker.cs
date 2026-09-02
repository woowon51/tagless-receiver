using System.Collections.Concurrent;
using System.Net.Http.Json;

namespace tagless_receiver.Services;

/// <summary>
/// BLE 수신을 기준으로 출석의 START / END를 판정한다.
///
/// 역할:
///   - 최초 BLE 수신       → START
///   - BLE 계속 수신       → 마지막 수신시각만 메모리 갱신
///   - 12분 이상 미수신    → END
///   - END 후 재수신       → 새로운 START
///
/// 중요:
///   - attendance_record DB 처리는 직접 하지 않는다.
///   - 서버 attendance_transaction.py에 START / END만 전달한다.
///   - 지속적인 BLE 수신 기록은 기존 /receiver/ble-link가 담당한다.
/// </summary>
public sealed class AttendanceChecker : IDisposable
{
    private static readonly HttpClient httpClient = new();

    private const string AttendanceApiUrl =
        "https://tagless-api-production.up.railway.app/attendance/transaction";

    private static readonly TimeSpan AbsenceTimeout =
        TimeSpan.FromMinutes(12);

    private static readonly TimeSpan CheckInterval =
        TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<int, AttendanceState>
        states = new();

    private readonly System.Threading.Timer timer;

    private bool disposed;


    public AttendanceChecker()
    {
        timer = new System.Threading.Timer(
            CheckTimeouts,
            null,
            CheckInterval,
            CheckInterval
        );

        Logger.Write("[ATTENDANCE] AttendanceChecker 시작");
    }


    /// <summary>
    /// BleScanService가 Tagless Sender BLE를 확인할 때마다 호출한다.
    /// </summary>
    public void Seen(
        int senderDeviceId)
    {
        DateTime now = DateTime.UtcNow;

        AttendanceState state =
            states.GetOrAdd(
                senderDeviceId,
                _ => new AttendanceState()
            );

        bool shouldStart = false;

        lock (state)
        {
            state.LastSeenAt = now;

            if (!state.IsPresent)
            {
                state.IsPresent = true;
                shouldStart = true;
            }
        }

        if (shouldStart)
        {
            Logger.Write(
                "[ATTENDANCE] START 판정: " +
                $"sender_device_id={senderDeviceId}"
            );

            _ = SendTransactionAsync(
                senderDeviceId,
                "START",
                now
            );
        }
    }


    /// <summary>
    /// 30초마다 현재 출석 중인 Sender의 마지막 BLE 수신시각을 검사한다.
    /// 12분 이상 수신이 없으면 END 처리한다.
    /// </summary>
    private void CheckTimeouts(
        object? stateObject)
    {
        DateTime now = DateTime.UtcNow;

        foreach (
            KeyValuePair<int, AttendanceState> item
            in states
        )
        {
            int senderDeviceId = item.Key;
            AttendanceState state = item.Value;

            bool shouldEnd = false;
            DateTime lastSeenAt = DateTime.MinValue;

            lock (state)
            {
                if (!state.IsPresent)
                    continue;

                if (
                    now - state.LastSeenAt
                    < AbsenceTimeout
                )
                {
                    continue;
                }

                state.IsPresent = false;

                lastSeenAt = state.LastSeenAt;
                shouldEnd = true;
            }

            if (shouldEnd)
            {
                Logger.Write(
                    "[ATTENDANCE] END 판정: " +
                    $"sender_device_id={senderDeviceId}, " +
                    $"last_seen_at={lastSeenAt:O}"
                );

                _ = SendTransactionAsync(
                    senderDeviceId,
                    "END",
                    lastSeenAt
                );
            }
        }
    }


    /// <summary>
    /// 출석 상태변경만 서버로 전달한다.
    /// START = 새 attendance_record 시작
    /// END   = 현재 attendance_record 종료
    /// </summary>
    private async Task SendTransactionAsync(
        int senderDeviceId,
        string eventType,
        DateTime eventAt)
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
                    "[ATTENDANCE] receiver_device_id 없음"
                );

                return;
            }

            var payload = new
            {
                receiver_device_id =
                    config.receiver_device_id,

                sender_device_id =
                    senderDeviceId,

                event_type =
                    eventType,

                event_at =
                    eventAt
            };

            using HttpResponseMessage response =
                await httpClient.PostAsJsonAsync(
                    AttendanceApiUrl,
                    payload
                );

            string responseText =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Logger.Write(
                    "[ATTENDANCE] 서버 전송 실패: " +
                    $"event={eventType}, " +
                    $"sender_device_id={senderDeviceId}, " +
                    $"status={(int)response.StatusCode}, " +
                    $"body={responseText}"
                );

                return;
            }

            Logger.Write(
                "[ATTENDANCE] 서버 전송 성공: " +
                $"event={eventType}, " +
                $"sender_device_id={senderDeviceId}"
            );
        }
        catch (Exception ex)
        {
            Logger.Error(
                "[ATTENDANCE] SendTransactionAsync 실패",
                ex
            );
        }
    }


    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        timer.Dispose();

        Logger.Write(
            "[ATTENDANCE] AttendanceChecker 종료"
        );
    }


    private sealed class AttendanceState
    {
        public DateTime LastSeenAt { get; set; }

        public bool IsPresent { get; set; }
    }
}