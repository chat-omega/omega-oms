using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace ZeroPlus.Models.Pools;

public abstract class ObjectPoolPreAllocator<T> : IHostedService where T : class
{
    private readonly MonitoredObjectPool<T> _pool;
    private readonly int _count;

    protected ObjectPoolPreAllocator(MonitoredObjectPool<T> pool, int count)
    {
        _pool = pool;
        _count = count;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_count <= 0)
        {
            return Task.CompletedTask;
        }

        var tempReferences = new T[_count];

        for (var i = 0; i < _count; i++)
        {
            tempReferences[i] = _pool.Get();
        }

        for (var i = 0; i < _count; i++)
        {
            _pool.Return(tempReferences[i]);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}