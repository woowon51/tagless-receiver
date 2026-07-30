using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using tagless_receiver.Models;

namespace tagless_receiver.Services;

public static class RegistrationService
{
    private static readonly HttpClient httpClient = new();

    private const string ApiBaseUrl =
        "https://tagless-api-production.up.railway.app/receiver";

    private const string RegistrationApiUrl =
        ApiBaseUrl + "/config";

    private const string RegisterCheckApiUrl =
        ApiBaseUrl + "/register-check";

    private const string CleanupApiUrl =
        ApiBaseUrl + "/cleanup";

    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<ReceiverConfig?> RegisterCheckAsync(
        string receiverDeviceId,
        string hardwareFingerprint)
    {
        try
        {
            var payload = new
            {
                receiver_device_id = receiverDeviceId,
                hardware_fingerprint = hardwareFingerprint
            };

            Logger.Write(
                $"POST {RegisterCheckApiUrl} " +
                $"receiver_device_id={receiverDeviceId}"
            );

            using HttpResponseMessage response =
                await httpClient.PostAsJsonAsync(
                    RegisterCheckApiUrl,
                    payload
                );

            Logger.Write($"Status = {(int)response.StatusCode}");

            string json =
                await response.Content.ReadAsStringAsync();

            Logger.Write(json);

            response.EnsureSuccessStatusCode();

            return JsonSerializer.Deserialize<ReceiverConfig>(
                json,
                jsonOptions
            );
        }
        catch (Exception ex)
        {
            Logger.Error(
                "[RegistrationService] RegisterCheckAsync 실패",
                ex
            );

            return null;
        }
    }

    public static async Task<int?> CleanupPendingAsync()
    {
        try
        {
            Logger.Write($"POST {CleanupApiUrl}");

            using HttpResponseMessage response =
                await httpClient.PostAsync(
                    CleanupApiUrl,
                    content: null
                );

            Logger.Write($"Status = {(int)response.StatusCode}");

            string json =
                await response.Content.ReadAsStringAsync();

            Logger.Write(json);

            response.EnsureSuccessStatusCode();

            CleanupResponse? result =
                JsonSerializer.Deserialize<CleanupResponse>(
                    json,
                    jsonOptions
                );

            return result?.deleted_count;
        }
        catch (Exception ex)
        {
            Logger.Error(
                "[RegistrationService] CleanupPendingAsync 실패",
                ex
            );

            return null;
        }
    }

    public static async Task<ReceiverConfig?> GetRegistrationAsync(
        string receiverDeviceId)
    {
        try
        {
            string url =
                $"{RegistrationApiUrl}" +
                $"?receiver_device_id=" +
                $"{Uri.EscapeDataString(receiverDeviceId)}";

            Logger.Write($"GET {url}");

            using HttpResponseMessage response =
                await httpClient.GetAsync(url);

            Logger.Write($"Status = {(int)response.StatusCode}");

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            string json =
                await response.Content.ReadAsStringAsync();

            Logger.Write(json);

            response.EnsureSuccessStatusCode();

            return JsonSerializer.Deserialize<ReceiverConfig>(
                json,
                jsonOptions
            );
        }
        catch (Exception ex)
        {
            Logger.Error(
                "[RegistrationService] GetRegistrationAsync 실패",
                ex
            );

            return null;
        }
    }

    private sealed class CleanupResponse
    {
        public string? status { get; set; }
        public int deleted_count { get; set; }
    }
}
