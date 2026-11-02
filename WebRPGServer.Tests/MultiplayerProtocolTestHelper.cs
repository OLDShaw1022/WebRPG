namespace WebRPGServer.Tests;

// 提供多人協定測試的拒絕與條件斷言。
internal static class MultiplayerProtocolTestHelper
{
    public static void Reject(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Expected protocol rejection.");
    }

    public static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
