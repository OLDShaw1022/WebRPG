namespace WebRPGServer;

// 表示由伺服器確認的固定帳號身分。
public sealed record AccountIdentity(string AccountId, string Username);
