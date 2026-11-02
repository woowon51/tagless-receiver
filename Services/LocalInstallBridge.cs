using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
namespace tagless_receiver.Services;
// Loopback only. TcpListener avoids a machine-wide HttpListener URL ACL.
public static class LocalInstallBridge
{
    public static async Task<JsonElement> PairAsync()
    {
        Logger.Trace("cs_local_bridge", "LocalInstallBridge", "PairAsync", "IN", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        var listener = new TcpListener(IPAddress.Loopback, 17843);
        listener.Start();
        Logger.Trace("cs_local_bridge", "LocalInstallBridge", "PairAsync", "LISTENER START", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        try
        {
            while (true)
            {
                using var client = await listener.AcceptTcpClientAsync(timeout.Token);
                Logger.Trace("cs_local_bridge", "LocalInstallBridge", "PairAsync", "REQUEST RECEIVED", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(3));
                var stream = client.GetStream();
                try
                {
                    var bytes = new List<byte>(); var b = new byte[1];
                    while (bytes.Count < 8192)
                    {
                        if (await stream.ReadAsync(b, deadline.Token) == 0) break;
                        bytes.Add(b[0]);
                        if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.TakeLast(4).ToArray()) == "\r\n\r\n") break;
                    }
                    var lines = Encoding.ASCII.GetString(bytes.ToArray()).Split("\r\n");
                    var headers = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var line in lines.Skip(1).Where(x => x.Contains(':')))
                    {
                        var parts = line.Split(':',2);
                        if (!headers.TryAdd(parts[0], parts[1].Trim())) throw new InvalidDataException();
                    }
                    if (headers.GetValueOrDefault("Origin") != "https://taglesscheck.com" || headers.GetValueOrDefault("Host") != "127.0.0.1:17843")
                    { await Respond(stream,403,false,deadline.Token); Logger.Trace("cs_local_bridge", "LocalInstallBridge", "PairAsync", "HTTP(403)", "Origin/Host 검사 실패 응답", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null); continue; }
                    if (lines[0] == "OPTIONS /pair HTTP/1.1")
                    { await Respond(stream,204,true,deadline.Token); Logger.Trace("cs_local_bridge", "LocalInstallBridge", "PairAsync", "HTTP(204)", "OPTIONS 응답", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null); continue; }
                    if (lines[0] != "POST /pair HTTP/1.1" || headers.GetValueOrDefault("Content-Type") != "application/json"
                        || headers.ContainsKey("Transfer-Encoding") || !int.TryParse(headers.GetValueOrDefault("Content-Length"),out int length)
                        || length < 1 || length > 4096)
                    { await Respond(stream,400,true,deadline.Token); Logger.Trace("cs_local_bridge", "LocalInstallBridge", "PairAsync", "HTTP(400)", "요청 형식 오류 응답", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null); continue; }
                    var body = new byte[length]; await stream.ReadExactlyAsync(body,deadline.Token);
                    var json = JsonDocument.Parse(body).RootElement.Clone();
                    // Pairing credentials come from the browser; claim validates them on the server.
                    if (json.ValueKind != JsonValueKind.Object
                        || !json.TryGetProperty("request_id", out var requestId)
                        || requestId.ValueKind != JsonValueKind.String
                        || string.IsNullOrWhiteSpace(requestId.GetString())
                        || !json.TryGetProperty("token", out var browserToken)
                        || browserToken.ValueKind != JsonValueKind.String
                        || string.IsNullOrWhiteSpace(browserToken.GetString())
                        || !json.TryGetProperty("browser_proof", out var browserProof)
                        || browserProof.ValueKind != JsonValueKind.String
                        || string.IsNullOrWhiteSpace(browserProof.GetString()))
                    { await Respond(stream,400,true,deadline.Token); Logger.Trace("cs_local_bridge", "LocalInstallBridge", "PairAsync", "HTTP(400)", "pair 필수 필드 형식 오류 응답", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null); continue; }
                    if (!System.Text.RegularExpressions.Regex.IsMatch(browserToken.GetString()!, @"\A[a-f0-9]{64}\z"))
                    { await Respond(stream,403,true,deadline.Token); Logger.Trace("cs_local_bridge", "LocalInstallBridge", "PairAsync", "HTTP(403)", "token 검증 실패 응답", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null); continue; }
                    await Respond(stream,200,true,deadline.Token);
                    Logger.Trace("cs_local_bridge", "LocalInstallBridge", "PairAsync", "HTTP(200)", "정상 pair 응답", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
                    Logger.Trace("cs_local_bridge", "LocalInstallBridge", "PairAsync", "OUT", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
                    return json;
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
                { /* Ignore malformed clients; overall deadline remains. */ }
            }
        }
        finally
        {
            if (timeout.IsCancellationRequested)
            {
                Logger.Trace("cs_local_bridge", "LocalInstallBridge", "PairAsync", "TIMEOUT", "전체 15분 제한 도달", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
            }
            Logger.Trace("cs_local_bridge", "LocalInstallBridge", "PairAsync", "LISTENER STOP", "", businessId: null, classId: null, businessMemberId: null, staffId: null, senderDeviceId: null, receiverDeviceId: null);
            listener.Stop();
        }
    }
    private static async Task Respond(NetworkStream stream,int status,bool cors,CancellationToken ct)
    {
        string h = cors ? "Access-Control-Allow-Origin: https://taglesscheck.com\r\nAccess-Control-Allow-Methods: POST, OPTIONS\r\nAccess-Control-Allow-Headers: Content-Type\r\nAccess-Control-Allow-Private-Network: true\r\nVary: Origin\r\n" : "";
        await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Result\r\n{h}Content-Length: 0\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n"),ct);
    }
}
