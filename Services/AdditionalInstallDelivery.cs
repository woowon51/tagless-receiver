using System.Net.Http.Json;
using System.Text.Json;
using tagless_receiver.Models;
namespace tagless_receiver.Services;
// Common delivery for WELCOME, ADDITIONAL and REINSTALL.
public static class AdditionalInstallDelivery
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string Api = "https://taglesscheck.com/receiver/install-request/";
    public static async Task<ReceiverConfig> RunAsync()
    {
        Logger.Trace("cs_addinstall", "AdditionalInstallDelivery", "RunAsync", "IN", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        var pair = await LocalInstallBridge.PairAsync();
        Logger.Trace("cs_addinstall", "AdditionalInstallDelivery", "RunAsync", "PAIR SUCCESS", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        string rid = pair.GetProperty("request_id").GetString() ?? "";
        string token = pair.GetProperty("token").GetString() ?? "";
        string browserProof = pair.GetProperty("browser_proof").GetString() ?? "";
        Logger.Trace("cs_addinstall", "AdditionalInstallDelivery", "RunAsync", "TOKEN_VALID", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        var config = ConfigService.LoadOrCreate();
        Logger.Trace("cs_addinstall", "AdditionalInstallDelivery", "RunAsync", "CONFIG_LOADED", "", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: config.receiver_device_id);
        Logger.Trace("cs_addinstall", "AdditionalInstallDelivery", "RunAsync", "CLAIM IN", "", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: config.receiver_device_id);
        using var claim = await Http.PostAsJsonAsync(Api + "claim",new {
            request_id=rid, token, browser_proof=browserProof,
            hardware_fingerprint=HardwareFingerprintService.Create(), install_key=InstallIdentity.Key(),
            saved_device_id=config.receiver_device_id });
        Logger.Trace("cs_addinstall", "AdditionalInstallDelivery", "RunAsync", $"SERVER HTTP({(int)claim.StatusCode})", "claim 응답", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: config.receiver_device_id);
        if (!claim.IsSuccessStatusCode) throw new InvalidOperationException("PC 등록 요청을 확인할 수 없습니다. 브라우저에서 설치 화면을 다시 열어주세요.");
        Logger.Trace("cs_addinstall", "AdditionalInstallDelivery", "RunAsync", "CLAIM ACCEPTED", "", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: config.receiver_device_id);
        var result = await ResumeAsync(rid);
        Logger.Trace("cs_addinstall", "AdditionalInstallDelivery", "RunAsync", "OUT", "", businessId: result.business_id, classId: result.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: result.receiver_device_id);
        return result;
    }
    public static async Task<ReceiverConfig> ResumeAsync(string rid)
    {
        Logger.Trace("cs_addinstall", "AdditionalInstallDelivery", "ResumeAsync", "IN", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        var payload=new {request_id=rid,install_key=InstallIdentity.Key(),hardware_fingerprint=HardwareFingerprintService.Create()};
        Logger.Trace("cs_addinstall", "AdditionalInstallDelivery", "ResumeAsync", "RESULT_WAIT IN", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        while(true)
        {
            using var response=await Http.PostAsJsonAsync(Api+"result",payload);
            if(!response.IsSuccessStatusCode)
            {
                Logger.Trace("cs_addinstall", "AdditionalInstallDelivery", "ResumeAsync", $"SERVER HTTP({(int)response.StatusCode})", "result 오류 응답", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
                throw new InvalidOperationException("설치 요청이 만료되었거나 권한이 변경되었습니다. 화면을 다시 여세요.");
            }
            var data=await response.Content.ReadFromJsonAsync<JsonElement>();
            if(data.GetProperty("ready").GetBoolean())
            {
                Logger.Trace("cs_addinstall", "AdditionalInstallDelivery", "ResumeAsync", "RESULT READY", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
                var config=data.GetProperty("config").Deserialize<ReceiverConfig>() ?? throw new InvalidDataException();
                Logger.Trace("cs_addinstall", "AdditionalInstallDelivery", "ResumeAsync", "CONFIG_RECEIVED", "", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: config.receiver_device_id);

                ConfigService.Save(config);
                Logger.Trace("cs_addinstall", "AdditionalInstallDelivery", "ResumeAsync", "CONFIG_SAVED", "로컬 ConfigService.Save 정상 반환", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: config.receiver_device_id);
                Logger.Trace("cs_addinstall", "AdditionalInstallDelivery", "ResumeAsync", "ACK IN", "", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: config.receiver_device_id);
                using var ack=await Http.PostAsJsonAsync(Api+"ack",payload);
                Logger.Trace("cs_addinstall", "AdditionalInstallDelivery", "ResumeAsync", $"SERVER HTTP({(int)ack.StatusCode})", "ack 응답", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: config.receiver_device_id);
                ack.EnsureSuccessStatusCode();
                Logger.Trace("cs_addinstall", "AdditionalInstallDelivery", "ResumeAsync", "ACK SUCCESS", "", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: config.receiver_device_id);
                Logger.Trace("cs_addinstall", "AdditionalInstallDelivery", "ResumeAsync", "OUT", "", businessId: config.business_id, classId: config.class_id, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: config.receiver_device_id);
                return config;
            }
            await Task.Delay(1500);
        }
    }
}

