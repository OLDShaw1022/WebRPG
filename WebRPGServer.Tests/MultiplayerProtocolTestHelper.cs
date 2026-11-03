using NUnit.Framework;
namespace WebRPGServer.Tests;

// 提供多人協定測試的拒絕與條件斷言。
internal static class MultiplayerProtocolTestHelper
{
    public static void Reject(Action action)
    {
        Assert.That(action, Throws.InstanceOf<ArgumentException>(), "Expected protocol rejection.");
    }

    public static void Check(bool condition, string message)
    {
        Assert.That(condition, Is.True, message);
    }
}
