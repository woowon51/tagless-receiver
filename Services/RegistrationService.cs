using System.Net.Http.Json;
using tagless_receiver.Models;

namespace tagless_receiver.Services;

public static class RegistrationService
{
    private static readonly HttpClient httpClient = new();

    // TODO:
    // 실제 Railway 서버 주소와 Receiver 조회 API 경로로 교체
    private const string RegistrationApiUrl =
        "https://tagless-api-production.up.railway.app/receiver/config";

    public static async Task<ReceiverConfig?> GetRegistrationAsync(
        string receiverDeviceId)
    {
        try
        {
            string url =
                $"{RegistrationApiUrl}?receiver_device_id={Uri.EscapeDataString(receiverDeviceId)}";

            Logger.Write($"GET {url}");

            HttpResponseMessage response =
                await httpClient.GetAsync(url);

            Logger.Write($"Status = {(int)response.StatusCode}");

            // 아직 서버에 등록되지 않은 수신장치
            if (response.StatusCode ==
                System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            string json =
                await response.Content.ReadAsStringAsync();

                Logger.Write(json);

            ReceiverConfig? serverConfig =
                await response.Content
                    .ReadFromJsonAsync<ReceiverConfig>();

            return serverConfig;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[RegistrationService ERROR] {ex}"
            );
            // 서버 연결 실패 시 Receiver 프로그램은 종료하지 않음
            // 이후 재시도 구조를 붙일 예정
            return null;
        }
    }
}