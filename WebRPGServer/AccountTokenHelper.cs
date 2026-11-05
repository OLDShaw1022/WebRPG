using System.Security.Cryptography;
using System.Text;

namespace WebRPGServer;

// 產生高熵登入憑證並統一雜湊與有效期限。
public static class AccountTokenHelper
{
    public const int SessionLifetimeHours = 12;
    public const int RememberedLifetimeDays = 30;
    public const int MaximumTokenLength = 128;
    private const int TokenBytes = 32;

    public static string Create() => Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenBytes));
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
