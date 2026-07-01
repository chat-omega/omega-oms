using System.Threading;
using Microsoft.Extensions.ObjectPool;

namespace ZeroPlus.Models.Pools;

public class MonitoredObjectPool<T> : ObjectPool<T> where T : class
{
    private readonly ObjectPool<T> _innerPool;
    private readonly MeasuredPooledObjectPolicy<T> _policy;
    private int _inUseCount;

    public MonitoredObjectPool(ObjectPool<T> innerPool, MeasuredPooledObjectPolicy<T> policy)
    {
        _innerPool = innerPool;
        _policy = policy;
    }

    public int InUseCount => _inUseCount;
    public int TotalAllocated => _policy.TotalAllocated;

    public override T Get()
    {
        var item = _innerPool.Get();
        Interlocked.Increment(ref _inUseCount);
        return item;
    }

    public override void Return(T obj)
    {
        _innerPool.Return(obj);
        Interlocked.Decrement(ref _inUseCount);
    }
}