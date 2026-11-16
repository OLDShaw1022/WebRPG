namespace WebRPGServer;

// 追蹤目前 WebSocket 使用的憑證，讓帳號寫入可撤銷對應連線的驗證結果。
public sealed partial class PostgresAccountStore
{
    private readonly object _lobbySessionGate = new();
    private readonly Dictionary<LobbyAccountSession, (string TokenHash, Guid? RememberedSessionId)> _lobbySessions =
        new();
    private long _lobbyRevocationVersion;

    internal async Task<LobbyAccountSession?> AuthenticateLobbyAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token) || token.Length > AccountTokenHelper.MaximumTokenLength) return null;

        string tokenHash = AccountTokenHelper.Hash(token);
        while (true)
        {
            long version;
            lock (_lobbySessionGate) version = _lobbyRevocationVersion;

            var session = await ReadAccountSessionAsync(tokenHash, cancellationToken);
            if (session is null) return null;

            lock (_lobbySessionGate)
            {
                // 若查詢期間有憑證撤銷，重新讀取，避免把已失效的查詢結果加入追蹤。
                if (version != _lobbyRevocationVersion) continue;

                var (identity, expiresAt, rememberedSessionId) = session.Value;
                LobbyAccountSession lobbySession = new(identity, expiresAt, ReleaseLobbySession);
                _lobbySessions.Add(lobbySession, (tokenHash, rememberedSessionId));
                return lobbySession;
            }
        }
    }

    private void RevokeLobbySessions(Predicate<(string TokenHash, Guid? RememberedSessionId)> matches)
    {
        lock (_lobbySessionGate)
        {
            _lobbyRevocationVersion++;
            foreach (var (session, credentials) in _lobbySessions)
            {
                if (matches(credentials)) session.Revoke();
            }
        }
    }

    private void ReleaseLobbySession(LobbyAccountSession session)
    {
        lock (_lobbySessionGate) _lobbySessions.Remove(session);
    }
}
