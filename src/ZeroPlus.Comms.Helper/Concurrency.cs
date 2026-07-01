
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroPlus.Comms.Helper.Concurrency
{
    public class AsyncLock : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        public async Task<IDisposable> LockAsync()
        {
            await _semaphore.WaitAsync();
            return this;
        }
        public void Dispose() => _semaphore.Release();
    }
}
