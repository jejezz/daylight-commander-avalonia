namespace DaylightCommander.Services;

/// <summary>호스트 키 지문을 앱 설정에 기억한다 (<see cref="IHostKeyStore"/>).</summary>
public sealed class SettingsHostKeyStore(SettingsController settings) : IHostKeyStore
{
    public string? Get(string hostPort) => settings.Value.KnownHostKeys.TryGetValue(hostPort, out var fingerprint) ? fingerprint : null;

    public void Set(string hostPort, string fingerprint) =>
        settings.Update(s => s with { KnownHostKeys = new Dictionary<string, string>(s.KnownHostKeys) { [hostPort] = fingerprint } });
}
