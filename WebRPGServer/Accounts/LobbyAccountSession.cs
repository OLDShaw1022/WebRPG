namespace WebRPGServer;

// 保留 WebSocket 已驗證的帳號身分，並在憑證撤銷或到期時拒絕後續操作。
internal sealed class LobbyAccountSession : IDisposable
{
    private readonly DateTimeOffset _expiresAt;
    private readonly Action<LobbyAccountSession>? _release;
    private volatile bool _revoked;

    internal AccountIdentity Identity { get; }

    internal LobbyAccountSession(
        AccountIdentity identity, DateTimeOffset expiresAt, Action<LobbyAccountSession>? release = null)
    {
        Identity = identity;
        _expiresAt = expiresAt;
        _release = release;
    }

    internal bool IsValidAt(DateTimeOffset timestamp) => !_revoked && timestamp < _expiresAt;

    internal void Revoke() => _revoked = true;

    public void Dispose() => _release?.Invoke(this);
}
