using System.Security.Cryptography;

namespace WebRPGServer;

// 以隨機鹽值與 PBKDF2 保存及驗證密碼雜湊。
public static class AccountPasswordHelper
{
    private const int Iterations = 600000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int HashParts = 3;

    public static string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string encoded, string password)
    {
        string[] parts = encoded.Split(':');
        if (parts.Length != HashParts || !int.TryParse(parts[0], out int iterations) || iterations != Iterations)
            return false;
        try
        {
            byte[] salt = Convert.FromBase64String(parts[1]);
            byte[] expected = Convert.FromBase64String(parts[2]);
            if (salt.Length != SaltBytes || expected.Length != HashBytes) return false;
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashBytes);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
}
