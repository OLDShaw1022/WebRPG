namespace WebRPGServer;

// 保存註冊表單內容。
public sealed record AccountRegistration(string Email, string Password, string Username);

// 保存登入表單內容。
public sealed record AccountLogin(string Email, string Password);

// 表示由伺服器確認的固定帳號身分。
public sealed record AccountIdentity(string AccountId, string Username);

// 傳回短期登入憑證與帳號公開資料。
public sealed record AccountSession(string AccessToken, string AccountId, string Username, DateTimeOffset ExpiresAt);
