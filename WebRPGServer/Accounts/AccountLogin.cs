namespace WebRPGServer;

// 保存登入表單內容。
public sealed record AccountLogin(string Email, string Password, bool RememberMe = false);
