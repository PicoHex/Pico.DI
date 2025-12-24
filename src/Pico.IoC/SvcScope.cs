namespace Pico.IoC;

public sealed class SvcScope(ConcurrentDictionary<Type, List<SvcDescriptor>> descriptorCache)
    : ISvcScope
{
    private readonly ConcurrentDictionary<SvcDescriptor, object> _scopedInstances = new();
    private readonly ConcurrentDictionary<SvcDescriptor, Lock> _singletonLocks = new();
    private bool _disposed;

    public ISvcScope CreateScope()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new SvcScope(descriptorCache);
    }

    public object GetService(Type serviceType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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
            SvcLifetime.Scoped => GetOrAddScopedInstance(resolver),
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
        if (resolver.SingleInstance != null)
            return resolver.SingleInstance;

        var singletonLock = _singletonLocks.GetOrAdd(resolver, _ => new Lock());
        lock (singletonLock)
        {
            if (resolver.SingleInstance != null)
                return resolver.SingleInstance;

            resolver.SingleInstance =
                resolver.Factory != null
                    ? resolver.Factory(this)
                    : throw new PicoIocException(
                        $"No factory or instance registered for singleton service '{serviceType.FullName}'."
                    );

            return resolver.SingleInstance;
        }
    }

    public IEnumerable<object> GetServices(Type serviceType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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
                SvcLifetime.Scoped => GetOrAddScopedInstance(resolver),
                _
                    => throw new ArgumentOutOfRangeException(
                        nameof(resolver.Lifetime),
                        resolver.Lifetime,
                        $"Unknown service lifetime '{resolver.Lifetime}'."
                    )
            }
        );
    }

    private object GetOrAddScopedInstance(SvcDescriptor resolver) =>
        _scopedInstances.GetOrAdd(
            resolver,
            desc =>
                desc.Factory != null
                    ? desc.Factory(this)
                    : throw new PicoIocException(
                        $"No factory registered for scoped service '{desc.ServiceType.FullName}'."
                    )
        );

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        if (disposing)
        {
            foreach (var svc in _scopedInstances.Values)
            {
                if (svc is IDisposable disposable)
                    disposable.Dispose();
            }
            _scopedInstances.Clear();
        }
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
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
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
