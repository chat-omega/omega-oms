using System;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroPlus.Models.Extensions
{
    public static class WaitHandleExtensions
    {
        public static Task<bool> WaitOneAsync(this ManualResetEventSlim manualResetEvent, CancellationToken cancellationToken = default)
        {
            return manualResetEvent.WaitOneAsync(cancellationToken, Timeout.Infinite);
        }

        public static Task<bool> WaitOneAsync(this ManualResetEventSlim manualResetEvent, int timeoutMilliseconds = Timeout.Infinite)
        {
            return manualResetEvent.WaitOneAsync(CancellationToken.None, timeoutMilliseconds);
        }

        public static Task<bool> WaitOneAsync(this ManualResetEventSlim manualResetEvent, CancellationToken cancellationToken, int timeoutMilliseconds)
        {
            return manualResetEvent.WaitHandle.WaitOneAsync(cancellationToken, timeoutMilliseconds);
        }

        public static Task<bool> WaitOneAsync(this WaitHandle waitHandle, int timeoutMilliseconds = Timeout.Infinite)
        {
            return waitHandle.WaitOneAsync(CancellationToken.None, timeoutMilliseconds);
        }

        public static Task<bool> WaitOneAsync(this WaitHandle waitHandle, CancellationToken cancellationToken, int timeoutMilliseconds = Timeout.Infinite)
        {
            if (waitHandle == null)
            {
                throw new ArgumentNullException(nameof(waitHandle));
            }
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            TimeSpan timeout = timeoutMilliseconds > Timeout.Infinite ? TimeSpan.FromMilliseconds(timeoutMilliseconds) : Timeout.InfiniteTimeSpan;

            RegisteredWaitHandle rwh = ThreadPool.RegisterWaitForSingleObject(waitHandle,
                (_, timedOut) =>
                {
                    if (timedOut)
                    {
                        tcs.TrySetResult(false);
                    }
                    else
                    {
                        tcs.TrySetResult(true);
                    }
                },
                null, timeout, true);

            Task<bool> task = tcs.Task;

            _ = task.ContinueWith(_ =>
            {
                rwh.Unregister(null);
            }, cancellationToken);

            return task;
        }
    }
}
