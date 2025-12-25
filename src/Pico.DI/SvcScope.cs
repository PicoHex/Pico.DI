namespace Pico.DI;

/// <summary>
/// Represents a service resolution scope that manages scoped service instances.
/// Implements <see cref="ISvcScope"/> and supports circular dependency detection.
/// When used with the Pico.DI.Gen source generator, all services are registered with
/// pre-compiled factories at compile time, making this implementation fully AOT-compatible.
/// No reflection is used - all factory delegates are generated at compile time via interceptors.
///
/// Supports decorator generic types: when a decorator is registered (e.g., Logger<>),
/// the source generator creates specific closed generic decorators (e.g., Logger<IUser>)
/// at compile time.
/// </summary>
public sealed class SvcScope : ISvcScope
{
    private readonly ConcurrentDictionary<Type, List<SvcDescriptor>> _descriptorCache;
    private readonly ConcurrentDictionary<Type, DecoratorMetadata>? _decoratorMetadata;
    private readonly ConcurrentDictionary<SvcDescriptor, object> _scopedInstances = new();
    private readonly ConcurrentDictionary<SvcDescriptor, Lock> _singletonLocks = new();
    private readonly AsyncLocal<HashSet<Type>> _resolutionStack = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new service resolution scope.
    /// </summary>
    /// <param name="descriptorCache">The container's service descriptor cache, shared across all scopes.</param>
    /// <param name="decoratorMetadata">Optional decorator metadata for source generator integration.</param>
    public SvcScope(
        ConcurrentDictionary<Type, List<SvcDescriptor>> descriptorCache,
        ConcurrentDictionary<Type, DecoratorMetadata>? decoratorMetadata = null
    )
    {
        _descriptorCache = descriptorCache;
        _decoratorMetadata = decoratorMetadata;
    }

    /// <inheritdoc />
    public ISvcScope CreateScope()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new SvcScope(_descriptorCache, _decoratorMetadata);
    }

    /// <inheritdoc />
    public object GetService(Type serviceType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Circular dependency detection
        var stack = _resolutionStack.Value ??=  [];
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
            if (serviceType.IsGenericType && !_descriptorCache.ContainsKey(serviceType))
            {
                var openGenericType = serviceType.GetGenericTypeDefinition();
                if (_descriptorCache.ContainsKey(openGenericType))
                {
                    throw new PicoDiException(
                        $"Open generic type '{openGenericType.FullName}' is registered, but closed type "
                            + $"'{serviceType.FullName}' was not detected at compile time. "
                            + "Ensure you call GetService<T> with this specific closed generic type in your code, "
                            + "or register a factory manually."
                    );
                }
            }

            if (!_descriptorCache.TryGetValue(serviceType, out var resolvers))
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
        if (!_descriptorCache.TryGetValue(serviceType, out var resolvers))
            throw new PicoDiException($"Service type '{serviceType.FullName}' is not registered.");

        return resolvers.Select(
            resolver =>
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
