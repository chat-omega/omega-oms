using System.Threading;
using Microsoft.Extensions.ObjectPool;

namespace ZeroPlus.Models.Pools;

public abstract class MeasuredPooledObjectPolicy<T> : PooledObjectPolicy<T> where T : class
{
    private int _totalAllocated;
    public int TotalAllocated => _totalAllocated;

    public override T Create()
    {
        Interlocked.Increment(ref _totalAllocated);
        return CreateInstance();
    }

    protected abstract T CreateInstance();
}