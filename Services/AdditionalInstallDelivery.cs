using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using tagless_receiver.Models;
namespace tagless_receiver.Services;
// Common delivery for WELCOME, ADDITIONAL and REINSTALL.
public static class AdditionalInstallDelivery
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string Api = "https://taglesscheck.com/receiver/install-request/";
    public static async Task<ReceiverConfig> RunAsync(string token)
    {
        if (!Regex.IsMatch(token,@"\A[a-f0-9]{64}\z")) throw new InvalidOperationException("설치 파일을 다시 다운로드해주세요.");
        var pair = await LocalInstallBridge.PairAsync(token);
        string rid = pair.GetProperty("request_id").GetString() ?? "";
        var config = ConfigService.LoadOrCreate();
        // Persist before sending: if commit succeeds but the response is lost,
        // next launch can fetch the result without replaying a single-use claim.
        InstallIdentity.PendingRequest=rid;
        using var claim = await Http.PostAsJsonAsync(Api + "claim",new {
            request_id=rid, token, browser_proof=pair.GetProperty("browser_proof").GetString(),
            hardware_fingerprint=HardwareFingerprintService.Create(), install_key=InstallIdentity.Key(),
            saved_device_id=config.receiver_device_id });
        if (!claim.IsSuccessStatusCode) throw new InvalidOperationException("PC 등록 요청을 확인할 수 없습니다. 브라우저에서 설치 화면을 다시 열어주세요.");
        return await ResumeAsync(rid);
    }
    public static async Task<ReceiverConfig> ResumeAsync(string rid)
    {
        var payload=new {request_id=rid,install_key=InstallIdentity.Key(),hardware_fingerprint=HardwareFingerprintService.Create()};
        var deadline=DateTime.UtcNow.AddMinutes(15);
        while(DateTime.UtcNow < deadline)
        {
            using var response=await Http.PostAsJsonAsync(Api+"result",payload);
            if(!response.IsSuccessStatusCode) throw new InvalidOperationException("설치 요청이 만료되었거나 권한이 변경되었습니다. 화면을 다시 여세요.");
            var data=await response.Content.ReadFromJsonAsync<JsonElement>();
            if(data.GetProperty("ready").GetBoolean())
            {
                var config=data.GetProperty("config").Deserialize<ReceiverConfig>() ?? throw new InvalidDataException();

                ConfigService.Save(config);
                using var ack=await Http.PostAsJsonAsync(Api+"ack",payload); ack.EnsureSuccessStatusCode();
                InstallIdentity.PendingRequest="";
                return config;
            }
            await Task.Delay(1500);
        }
        throw new TimeoutException("설치 시간이 만료되었습니다. 화면을 다시 여세요.");
    }
}

