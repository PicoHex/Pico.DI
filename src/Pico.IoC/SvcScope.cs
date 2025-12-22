namespace Pico.IoC;

public class SvcScope(ConcurrentDictionary<Type, List<SvcDescriptor>> descriptorCache) : ISvcScope
{
    private readonly ConcurrentDictionary<Type, List<object>> _scopedInstances = new();

    public ISvcScope CreateScope() => new SvcScope(descriptorCache);

    public object GetService(Type serviceType)
    {
        if (!descriptorCache.TryGetValue(serviceType, out var resolvers))
            throw new PicoIocException();
        var resolver = resolvers.LastOrDefault();
        if (resolver == null)
            throw new PicoIocException();
        return resolver.Lifetime switch
        {
            SvcLifetime.Transient
                => resolver.Factory != null ? resolver.Factory(this) : throw new PicoIocException(),
            SvcLifetime.Singleton
                => resolver.Instance ??=
                    resolver.Factory != null
                        ? resolver.Factory(this)
                        : throw new PicoIocException(),
            SvcLifetime.Scoped => GetOrAddScopedInstance(serviceType, resolver),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public IEnumerable<object> GetServices(Type serviceType)
    {
        if (!descriptorCache.TryGetValue(serviceType, out var resolvers))
            throw new PicoIocException();

        return resolvers.Select(
            resolver =>
                resolver.Lifetime switch
                {
                    SvcLifetime.Transient
                        => resolver.Factory != null
                            ? resolver.Factory(this)
                            : throw new PicoIocException(),
                    SvcLifetime.Singleton
                        => resolver.Instance ??=
                            resolver.Factory != null
                                ? resolver.Factory(this)
                                : throw new PicoIocException(),
                    SvcLifetime.Scoped => GetOrAddScopedInstance(serviceType, resolver),
                    _ => throw new ArgumentOutOfRangeException()
                }
        );
    }

    private object GetOrAddScopedInstance(Type serviceType, SvcDescriptor resolver) =>
        _scopedInstances
            .GetOrAdd(
                serviceType,
                _ =>
                {
                    var instance =
                        resolver.Factory != null
                            ? resolver.Factory(this)
                            : throw new PicoIocException();
                    return [instance];
                }
            )
            .First();

    public void Dispose()
    {
        foreach (var svc in _scopedInstances.SelectMany(p => p.Value))
        {
            if (svc is IDisposable disposable)
                disposable.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var svc in _scopedInstances.SelectMany(p => p.Value))
        {
            switch (svc)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync();
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
    }
}
