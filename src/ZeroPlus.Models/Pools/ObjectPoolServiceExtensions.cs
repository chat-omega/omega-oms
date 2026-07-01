using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ObjectPool;

namespace ZeroPlus.Models.Pools;
public static class ObjectPoolServiceExtensions
{
    public static IServiceCollection AddMonitoredObjectPool<T, TProvider, TPolicy, TPreAllocator>(this IServiceCollection services)
        where T : class
        where TProvider : ObjectPoolProvider
        where TPolicy : MeasuredPooledObjectPolicy<T>
        where TPreAllocator : class, IHostedService
    {
        services.AddSingleton<TProvider>();
        services.AddSingleton<TPolicy>();

        services.AddSingleton(sp =>
        {
            var provider = sp.GetRequiredService<TProvider>();
            var policy = sp.GetRequiredService<TPolicy>();

            var standardPool = provider.Create(policy);

            return new MonitoredObjectPool<T>(standardPool, policy);
        });

        services.AddSingleton<ObjectPool<T>>(sp => sp.GetRequiredService<MonitoredObjectPool<T>>());

        services.AddHostedService<TPreAllocator>();

        return services;
    }
}
