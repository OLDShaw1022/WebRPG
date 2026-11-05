using NUnit.Framework;
using WebRPGServer;

namespace WebRPGServer.Tests;

// 驗證帳號輸入、密碼雜湊與登入工作階段的預期行為。
public sealed class AccountValidationTests
{
    [Test]
    public void RegistrationNormalizesEmailAndAllowsDuplicateDisplayNames()
    {
        AccountRegistration first = new(" Player@Example.com ", "long-enough-password", "玩家");
        AccountRegistration second = new("other@example.com", "long-enough-password", "玩家");
        Assert.That(AccountValidation.Normalize(first).Email, Is.EqualTo("PLAYER@EXAMPLE.COM"));
        Assert.That(AccountValidation.Normalize(second).Username, Is.EqualTo("玩家"));
    }

    [TestCase("invalid", "long-enough-password", "玩家")]
    [TestCase("player@example.com", "short", "玩家")]
    [TestCase("player@example.com", "long-enough-password", " ")]
    public void RegistrationRejectsInvalidInput(string email, string password, string username)
    {
        Assert.Throws<ArgumentException>(() => AccountValidation.Normalize(new(email, password, username)));
    }

    [Test]
    public void PasswordHashesUseUniqueSaltAndRejectWrongPassword()
    {
        string first = AccountPasswordHelper.Hash("long-enough-password");
        string second = AccountPasswordHelper.Hash("long-enough-password");
        Assert.That(first, Is.Not.EqualTo(second));
        Assert.That(AccountPasswordHelper.Verify(first, "long-enough-password"), Is.True);
        Assert.That(AccountPasswordHelper.Verify(first, "wrong-password"), Is.False);
    }

    [TestCase("1234567", false)]
    [TestCase("12345678", true)]
    public void RegistrationRequiresAtLeastEightPasswordCharacters(string password, bool accepted)
    {
        AccountRegistration registration = new("player@example.com", password, "玩家");
        if (accepted) Assert.DoesNotThrow(() => AccountValidation.Normalize(registration));
        else Assert.Throws<ArgumentException>(() => AccountValidation.Normalize(registration));
    }
}
