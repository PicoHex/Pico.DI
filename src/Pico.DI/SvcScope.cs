namespace Pico.DI;

/// <summary>
/// High-performance service resolution scope using FrozenDictionary.
/// This implementation is used after Build() is called on the container.
///
/// PERFORMANCE CHARACTERISTICS:
/// ============================
/// - FrozenDictionary provides O(1) lookup with minimal overhead
/// - No concurrent dictionary overhead (read-only after build)
/// - Optimized for hot-path resolution with aggressive inlining
/// - Minimal allocations for scoped instance tracking
/// </summary>
public sealed class SvcScope : ISvcScope
{
    private readonly FrozenDictionary<Type, SvcDescriptor[]>? _descriptorCache;
    private readonly FrozenDictionary<Type, DecoratorMetadata>? _decoratorMetadata;
    private readonly ConcurrentDictionary<Type, SvcDescriptor[]>? _concurrentDescriptorCache;
    private readonly ConcurrentDictionary<Type, DecoratorMetadata>? _concurrentDecoratorMetadata;
    private readonly ConcurrentDictionary<SvcDescriptor, object> _scopedInstances = new();
    private static readonly ConcurrentDictionary<SvcDescriptor, object> SingletonLocks = new();
    private bool _disposing;

    /// <summary>
    /// Creates a new optimized service resolution scope.
    /// </summary>
    public SvcScope(
        FrozenDictionary<Type, SvcDescriptor[]> descriptorCache,
        FrozenDictionary<Type, DecoratorMetadata>? decoratorMetadata = null
    )
    {
        _descriptorCache = descriptorCache;
        _decoratorMetadata = decoratorMetadata;
        _concurrentDescriptorCache = null;
        _concurrentDecoratorMetadata = null;
    }

    // Backwards-compatible constructor for non-built container path
    public SvcScope(
        ConcurrentDictionary<Type, SvcDescriptor[]> descriptorCache,
        ConcurrentDictionary<Type, DecoratorMetadata>? decoratorMetadata = null
    )
    {
        _concurrentDescriptorCache = descriptorCache;
        _concurrentDecoratorMetadata = decoratorMetadata;
        _descriptorCache = null;
        _decoratorMetadata = null;
    }

    /// <inheritdoc />
    public ISvcScope CreateScope()
    {
        ObjectDisposedException.ThrowIf(_disposing, this);
        return _descriptorCache != null
            ? new SvcScope(_descriptorCache, _decoratorMetadata)
            : new SvcScope(_concurrentDescriptorCache!, _concurrentDecoratorMetadata);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object GetService(Type serviceType)
    {
        ObjectDisposedException.ThrowIf(_disposing, this);

        // Prefer frozen cache when available
        if (!TryGetResolvers(serviceType, out var resolvers))
        {
            // Check for open generic
            if (!serviceType.IsGenericType)
                throw new PicoDiException(
                    $"Service type '{serviceType.FullName}' is not registered."
                );
            var openGenericType = serviceType.GetGenericTypeDefinition();
            if (
                (_descriptorCache != null && _descriptorCache.ContainsKey(openGenericType))
                || (
                    _concurrentDescriptorCache != null
                    && _concurrentDescriptorCache.ContainsKey(openGenericType)
                )
            )
            {
                throw new PicoDiException(
                    $"Open generic type '{openGenericType.FullName}' is registered, but closed type "
                        + $"'{serviceType.FullName}' was not detected at compile time. "
                        + "Ensure you call GetService<T> with this specific closed generic type in your code, "
                        + "or register a factory manually."
                );
            }
            throw new PicoDiException($"Service type '{serviceType.FullName}' is not registered.");
        }

        // Get last registered descriptor (override pattern) - array access is very fast
        var resolver = resolvers![^1];

        return resolver.Lifetime switch
        {
            SvcLifetime.Transient => ResolveTransient(serviceType, resolver),
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private object ResolveTransient(Type serviceType, SvcDescriptor resolver)
    {
        return resolver.Factory != null
            ? resolver.Factory(this)
            : throw new PicoDiException(
                $"No factory registered for transient service '{serviceType.FullName}'. "
                    + "Use Pico.DI.Gen source generator or register with a factory delegate."
            );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private object GetOrCreateSingleton(Type serviceType, SvcDescriptor resolver)
    {
        // Fast path: volatile read to check if already created
        var instance = Volatile.Read(ref resolver.SingleInstance);
        if (instance != null)
            return instance;

        // Slow path: use lock to ensure single creation
        var lockObj = SingletonLocks.GetOrAdd(resolver, static _ => new object());
        lock (lockObj)
        {
            // Double-check after acquiring lock
            instance = Volatile.Read(ref resolver.SingleInstance);
            if (instance != null)
                return instance;

            instance =
                resolver.Factory != null
                    ? resolver.Factory(this)
                    : throw new PicoDiException(
                        $"No factory or instance registered for singleton service '{serviceType.FullName}'. "
                            + "Use Pico.DI.Gen source generator or register with a factory delegate."
                    );

            Volatile.Write(ref resolver.SingleInstance, instance);
            return instance;
        }
    }

    public IEnumerable<object> GetServices(Type serviceType)
    {
        ObjectDisposedException.ThrowIf(_disposing, this);
        if (!TryGetResolvers(serviceType, out var resolvers))
            throw new PicoDiException($"Service type '{serviceType.FullName}' is not registered.");

        return resolvers!
            .Select(
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
            )
            .ToArray();
    }

    private bool TryGetResolvers(Type serviceType, out SvcDescriptor[]? resolvers)
    {
        if (_descriptorCache != null)
        {
            if (_descriptorCache.TryGetValue(serviceType, out resolvers))
                return true;
            resolvers = null;
            return false;
        }

        if (_concurrentDescriptorCache != null)
        {
            return _concurrentDescriptorCache.TryGetValue(serviceType, out resolvers);
        }

        resolvers = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private object GetOrAddScopedInstance(SvcDescriptor resolver) =>
        _scopedInstances.GetOrAdd(
            resolver,
            static (desc, scope) =>
                desc.Factory != null
                    ? desc.Factory(scope)
                    : throw new PicoDiException(
                        $"No factory registered for scoped service '{desc.ServiceType.FullName}'. "
                            + "Use Pico.DI.Gen source generator or register with a factory delegate."
                    ),
            this
        );

    public void Dispose()
    {
        if (_disposing)
            return;
        _disposing = true;

        foreach (var svc in _scopedInstances.Values)
        {
            if (svc is IDisposable disposable)
                disposable.Dispose();
        }
        _scopedInstances.Clear();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposing)
            return;
        _disposing = true;

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
        GC.SuppressFinalize(this);
    }
}
