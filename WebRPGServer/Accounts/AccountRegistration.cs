namespace WebRPGServer;

// 保存註冊表單內容。
public sealed record AccountRegistration(string Email, string Password, string Username);
