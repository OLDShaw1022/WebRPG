using System.Net.Mail;

namespace WebRPGServer;

// 統一檢查帳號欄位並正規化 Email。
public static class AccountValidation
{
    public const int MinimumPasswordLength = 8;
    public const int MaximumPasswordLength = 128;
    public const int MaximumEmailLength = 254;
    public const int MaximumUsernameLength = 24;

    public static AccountRegistration Normalize(AccountRegistration registration)
    {
        string email = NormalizeEmail(registration.Email);
        string username = registration.Username?.Trim() ?? string.Empty;
        if (username.Length == 0 || username.Length > MaximumUsernameLength || username.Any(char.IsControl))
            throw new ArgumentException("使用者名稱須為 1～24 個字元，且不可含控制字元。");
        if (registration.Password is null || registration.Password.Length < MinimumPasswordLength ||
            registration.Password.Length > MaximumPasswordLength)
            throw new ArgumentException("密碼須為 8～128 個字元。");
        return registration with { Email = email, Username = username };
    }

    public static string NormalizeEmail(string? email)
    {
        string value = email?.Trim() ?? string.Empty;
        if (value.Length > MaximumEmailLength || !MailAddress.TryCreate(value, out MailAddress? parsed) ||
            parsed.Address != value || value.Any(char.IsControl))
            throw new ArgumentException("請輸入有效的 Email。");
        return value.ToUpperInvariant();
    }
}
