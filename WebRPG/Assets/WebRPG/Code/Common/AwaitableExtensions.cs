using System;
using System.Threading;
using UnityEngine;

namespace WebRPG
{
    public static class AwaitableExtensions
    {
        public static async Awaitable WaitForSecondsRealtimeAsync(float seconds, CancellationToken cancellationToken = default)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Awaitable.NextFrameAsync(cancellationToken);
                elapsed += Time.unscaledDeltaTime;
            }
        }

        public static void Forget(this Awaitable operation)
        {
            var awaiter = operation.GetAwaiter();

            if (awaiter.IsCompleted)
            {
                Complete();
            }
            else
            {
                awaiter.OnCompleted(Complete);
            }

            return;

            void Complete()
            {
                try
                {
                    awaiter.GetResult();
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
