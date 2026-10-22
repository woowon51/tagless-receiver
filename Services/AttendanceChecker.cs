namespace tagless_receiver.Services;

/// <summary>
/// Compatibility for existing scanner callers. The server alone decides
/// entry, movement and departure from reported real BLE observations.
/// </summary>
public sealed class AttendanceChecker : IDisposable
{
    public void Seen(int senderDeviceId) { }
    public void Dispose() { }
}
