namespace Pico.IoC;

public class SvcScope(ConcurrentDictionary<Type, List<SvcDescriptor>> descriptorCache) : ISvcScope
{
    private readonly ConcurrentDictionary<Type, object> _scopedInstances = new();
    private static readonly Lock SingletonLock = new();

    public ISvcScope CreateScope() => new SvcScope(descriptorCache);

    public object GetService(Type serviceType)
    {
        if (!descriptorCache.TryGetValue(serviceType, out var resolvers))
            throw new PicoIocException($"Service type '{serviceType.FullName}' is not registered.");
        var resolver =
            resolvers.LastOrDefault()
            ?? throw new PicoIocException(
                $"No service descriptor found for type '{serviceType.FullName}'."
            );
        return resolver.Lifetime switch
        {
            SvcLifetime.Transient
                => resolver.Factory != null
                    ? resolver.Factory(this)
                    : throw new PicoIocException(
                        $"No factory registered for transient service '{serviceType.FullName}'."
                    ),
            SvcLifetime.Singleton => GetOrCreateSingleton(serviceType, resolver),
            SvcLifetime.Scoped => GetOrAddScopedInstance(serviceType, resolver),
            _
                => throw new ArgumentOutOfRangeException(
                    nameof(resolver.Lifetime),
                    resolver.Lifetime,
                    $"Unknown service lifetime '{resolver.Lifetime}'."
                )
        };
    }

    private object GetOrCreateSingleton(Type serviceType, SvcDescriptor resolver)
    {
        if (resolver.Instance != null)
            return resolver.Instance;

        lock (SingletonLock)
        {
            if (resolver.Instance != null)
                return resolver.Instance;

            resolver.Instance =
                resolver.Factory != null
                    ? resolver.Factory(this)
                    : throw new PicoIocException(
                        $"No factory or instance registered for singleton service '{serviceType.FullName}'."
                    );

            return resolver.Instance;
        }
    }

    public IEnumerable<object> GetServices(Type serviceType)
    {
        if (!descriptorCache.TryGetValue(serviceType, out var resolvers))
            throw new PicoIocException($"Service type '{serviceType.FullName}' is not registered.");

        return resolvers.Select(resolver =>
            resolver.Lifetime switch
            {
                SvcLifetime.Transient
                    => resolver.Factory != null
                        ? resolver.Factory(this)
                        : throw new PicoIocException(
                            $"No factory registered for transient service '{serviceType.FullName}'."
                        ),
                SvcLifetime.Singleton => GetOrCreateSingleton(serviceType, resolver),
                SvcLifetime.Scoped => GetOrAddScopedInstance(serviceType, resolver),
                _
                    => throw new ArgumentOutOfRangeException(
                        nameof(resolver.Lifetime),
                        resolver.Lifetime,
                        $"Unknown service lifetime '{resolver.Lifetime}'."
                    )
            }
        );
    }

    private object GetOrAddScopedInstance(Type serviceType, SvcDescriptor resolver) =>
        _scopedInstances.GetOrAdd(
            serviceType,
            _ =>
                resolver.Factory != null
                    ? resolver.Factory(this)
                    : throw new PicoIocException(
                        $"No factory registered for scoped service '{serviceType.FullName}'."
                    )
        );

    public void Dispose()
    {
        foreach (var svc in _scopedInstances.Values)
        {
            if (svc is IDisposable disposable)
                disposable.Dispose();
        }
        _scopedInstances.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var svc in _scopedInstances.Values)
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
        _scopedInstances.Clear();
    }
}
