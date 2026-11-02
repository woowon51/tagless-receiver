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

    public static async Task<ReceiverConfig?> RegisterCheckAsync(string receiverDeviceId,string hardwareFingerprint)
    {
        try
        {
            using var response=await Http.PostAsJsonAsync(Url,new {
                receiver_device_id=receiverDeviceId,hardware_fingerprint=hardwareFingerprint,install_key=InstallIdentity.Key()});
            response.EnsureSuccessStatusCode();
            var config = await response.Content.ReadFromJsonAsync<ReceiverConfig>(new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
            lock (checkLogLock)
            {
                if (config is null)
                {
                    if (lastCheckSucceeded != false)
                    {
                        Logger.Trace("cs_registration", "RegistrationService", "RegisterCheckAsync", "REGISTER CHECK ERROR", "JSON NULL", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
                    }
                    lastCheckSucceeded = false;
                }
                else
                {
                    if (lastCheckSucceeded != true)
                    {
                        Logger.Trace("cs_registration", "RegistrationService", "RegisterCheckAsync", lastCheckSucceeded == false ? "REGISTER CHECK RECOVERED" : "REGISTER CHECK OK", "", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
                    }
                    lastCheckSucceeded = true;
                }
            }
            return config;
        }
        catch(Exception ex)
        {
            lock (checkLogLock)
            {
                if (lastCheckSucceeded != false)
                {
                    Logger.Trace("cs_registration", "RegistrationService", "RegisterCheckAsync", "REGISTER CHECK ERROR", ex is HttpRequestException h ? $"HTTP={(h.StatusCode.HasValue ? ((int)h.StatusCode.Value).ToString() : "NONE")}, Network={h.HttpRequestError}, Inner={h.InnerException?.GetType().Name ?? "NONE"}" : $"type={ex.GetType().Name}", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
                }
                lastCheckSucceeded = false;
            }
            return null;
        }
    }
}
