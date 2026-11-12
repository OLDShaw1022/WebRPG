namespace WebRPGServer;

// 傳回短期登入憑證與帳號公開資料。
public sealed record AccountSession(string AccessToken, string AccountId, string Username, DateTimeOffset ExpiresAt);
