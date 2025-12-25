namespace Pico.DI;

/// <summary>
/// Represents a service resolution scope that manages scoped service instances.
/// Implements <see cref="ISvcScope"/> and supports circular dependency detection.
/// When used with the Pico.DI.Gen source generator, all services are registered with
/// pre-compiled factories at compile time, making this implementation fully AOT-compatible.
/// No reflection is used - all factory delegates are generated at compile time via interceptors.
/// </summary>
public sealed class SvcScope(ConcurrentDictionary<Type, List<SvcDescriptor>> descriptorCache)
    : ISvcScope
{
    private readonly ConcurrentDictionary<SvcDescriptor, object> _scopedInstances = new();
    private readonly ConcurrentDictionary<SvcDescriptor, Lock> _singletonLocks = new();
    private readonly AsyncLocal<HashSet<Type>> _resolutionStack = new();
    private bool _disposed;

    /// <inheritdoc />
    public ISvcScope CreateScope()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new SvcScope(descriptorCache);
    }

    /// <inheritdoc />
    public object GetService(Type serviceType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Circular dependency detection
        var stack = _resolutionStack.Value ??= [];
        if (!stack.Add(serviceType))
        {
            var chain = string.Join(" -> ", stack.Select(t => t.Name)) + " -> " + serviceType.Name;
            throw new PicoDiException($"Circular dependency detected: {chain}");
        }

        try
        {
            // For open generics: the source generator generates closed generic factories
            // based on GetService<T> calls found at compile time. If a closed generic
            // is not pre-registered, it means it wasn't detected at compile time.
            if (serviceType.IsGenericType && !descriptorCache.ContainsKey(serviceType))
            {
                var openGenericType = serviceType.GetGenericTypeDefinition();
                if (descriptorCache.ContainsKey(openGenericType))
                {
                    throw new PicoDiException(
                        $"Open generic type '{openGenericType.FullName}' is registered, but closed type "
                            + $"'{serviceType.FullName}' was not detected at compile time. "
                            + "Ensure you call GetService<T> with this specific closed generic type in your code, "
                            + "or register a factory manually."
                    );
                }
            }

            if (!descriptorCache.TryGetValue(serviceType, out var resolvers))
                throw new PicoDiException(
                    $"Service type '{serviceType.FullName}' is not registered."
                );

            var resolver =
                resolvers.LastOrDefault()
                ?? throw new PicoDiException(
                    $"No service descriptor found for type '{serviceType.FullName}'."
                );

            return resolver.Lifetime switch
            {
                SvcLifetime.Transient
                    => resolver.Factory != null
                        ? resolver.Factory(this)
                        : throw new PicoDiException(
                            $"No factory registered for transient service '{serviceType.FullName}'. "
                                + "Use Pico.DI.Gen source generator or register with a factory delegate."
                        ),
                SvcLifetime.Singleton => GetOrCreateSingleton(serviceType, resolver),
                SvcLifetime.Scoped => GetOrAddScopedInstance(resolver),
                _
                    => throw new ArgumentOutOfRangeException(
                        nameof(SvcLifetime),
                        resolver.Lifetime,
                        $"Unknown service lifetime '{resolver.Lifetime}'."
                    )
            };
        }
        finally
        {
            stack.Remove(serviceType);
        }
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
                    : throw new PicoDiException(
                        $"No factory or instance registered for singleton service '{serviceType.FullName}'. "
                            + "Use Pico.DI.Gen source generator or register with a factory delegate."
                    );

            return resolver.SingleInstance;
        }
    }

    public IEnumerable<object> GetServices(Type serviceType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!descriptorCache.TryGetValue(serviceType, out var resolvers))
            throw new PicoDiException($"Service type '{serviceType.FullName}' is not registered.");

        return resolvers.Select(resolver =>
            resolver.Lifetime switch
            {
                SvcLifetime.Transient
                    => resolver.Factory != null
                        ? resolver.Factory(this)
                        : throw new PicoDiException(
                            $"No factory registered for transient service '{serviceType.FullName}'."
                        ),
                SvcLifetime.Singleton => GetOrCreateSingleton(serviceType, resolver),
                SvcLifetime.Scoped => GetOrAddScopedInstance(resolver),
                _
                    => throw new ArgumentOutOfRangeException(
                        nameof(SvcLifetime),
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
                    : throw new PicoDiException(
                        $"No factory registered for scoped service '{desc.ServiceType.FullName}'. "
                            + "Use Pico.DI.Gen source generator or register with a factory delegate."
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
