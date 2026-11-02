using UnityEngine;

namespace WebRPG.Tests
{
    // 建立已完成且可由呼叫端觀察一次的測試 Awaitable。
    internal static class TestAwaitableHelper
    {
        public static Awaitable Complete()
        {
            AwaitableCompletionSource source = new AwaitableCompletionSource();
            source.SetResult();
            return source.Awaitable;
        }
    }
}
