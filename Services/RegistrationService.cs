using System.Net.Http.Json;
using System.Text.Json;
using tagless_receiver.Models;
namespace tagless_receiver.Services;
public static class RegistrationService
{
    private static readonly HttpClient Http=new(){Timeout=TimeSpan.FromSeconds(10)};
    private const string Url="https://taglesscheck.com/receiver/register-check";
    public static async Task<ReceiverConfig?> RegisterCheckAsync(string receiverDeviceId,string hardwareFingerprint)
    {
        try
        {
            using var response=await Http.PostAsJsonAsync(Url,new {
                receiver_device_id=receiverDeviceId,hardware_fingerprint=hardwareFingerprint,install_key=InstallIdentity.Key()});
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ReceiverConfig>(new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
        }
        catch(Exception) {Logger.Write("[RegistrationService] 등록 확인 실패: 운영을 보류하고 재시도합니다.");return null;}
    }
}
