using System.Net.Http.Json;
using System.Text.Json;
using tagless_receiver.Models;
namespace tagless_receiver.Services;
public static class RegistrationService
{
    private static readonly HttpClient Http=new(){Timeout=TimeSpan.FromSeconds(10)};
    private const string Url="https://taglesscheck.com/receiver/register-check";
    private static readonly object checkLogLock = new();
    // null: no completed observation; false: unavailable; true: valid JSON response.
    private static bool? lastCheckSucceeded;
    private static long consecutiveFailures;

    // null means verification failed; registered=false (including HTTP 403) means stop BLE.
    public static async Task<ReceiverConfig?> RegisterCheckAsync(string receiverDeviceId,string hardwareFingerprint,int? businessId=null,int? classId=null)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        string stage = "identity";
        int? statusCode = null;
        string? installKey = null;
        try
        {
            installKey = InstallIdentity.Key();
            stage = "post";
            using var response=await Http.PostAsJsonAsync(Url,new {
                receiver_device_id=receiverDeviceId,hardware_fingerprint=hardwareFingerprint,install_key=installKey});
            stage = "status";
            statusCode = (int)response.StatusCode;
            response.EnsureSuccessStatusCode();
            stage = "json";
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            bool hasRegistered = false;
            if (json.ValueKind != JsonValueKind.Object)
                throw new JsonException();
            foreach (var property in json.EnumerateObject())
            {
                if (!string.Equals(property.Name, "registered", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (property.Value.ValueKind != JsonValueKind.True && property.Value.ValueKind != JsonValueKind.False)
                    throw new JsonException();
                hasRegistered = true;
            }
            if (!hasRegistered)
                throw new JsonException();
            var config = json.Deserialize<ReceiverConfig>(new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
            if (config is null)
                throw new JsonException();
            lock (checkLogLock)
            {
                if (lastCheckSucceeded != true)
                {
                    Logger.Trace("cs_registration", "RegistrationService", "RegisterCheckAsync", lastCheckSucceeded == false ? "REGISTER CHECK RECOVERED" : "REGISTER CHECK OK",
                        $"registered={config.registered.ToString().ToLowerInvariant()}, consecutive_failures={consecutiveFailures}, elapsed_ms={timer.ElapsedMilliseconds}, HTTP={statusCode}",
                        businessId: config.business_id ?? businessId, classId: config.class_id ?? classId, receiverDeviceId: receiverDeviceId);
                }
                consecutiveFailures = 0;
                lastCheckSucceeded = true;
            }
            return config;
        }
        catch(Exception ex)
        {
            lock (checkLogLock)
            {
                consecutiveFailures++;
                if (lastCheckSucceeded != false)
                {
                    Logger.Trace("cs_registration", "RegistrationService", "RegisterCheckAsync", "REGISTER CHECK ERROR",
                        $"stage={stage}, elapsed_ms={timer.ElapsedMilliseconds}, HTTP={statusCode?.ToString() ?? ((int?)(ex as HttpRequestException)?.StatusCode)?.ToString() ?? "NONE"}, consecutive_failures={consecutiveFailures}, {Logger.ExceptionDetails(ex, installKey, hardwareFingerprint)}",
                        businessId: businessId, classId: classId, receiverDeviceId: receiverDeviceId);
                }
                lastCheckSucceeded = false;
            }
            if (ex is HttpRequestException { StatusCode: System.Net.HttpStatusCode.Forbidden })
                return new ReceiverConfig { registered = false };
            return null;
        }
    }
}
