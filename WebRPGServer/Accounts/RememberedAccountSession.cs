namespace WebRPGServer;

// 將長效憑證保留於伺服器內部，僅由端點寫入 HttpOnly Cookie。
public sealed record RememberedAccountSession(AccountSession Session, string RefreshToken, DateTimeOffset RefreshExpiresAt);
