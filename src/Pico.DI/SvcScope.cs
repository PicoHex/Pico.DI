namespace Pico.DI;

/// <summary>
/// Represents a service scope that manages the lifetime of scoped service instances.
/// Supports hierarchical scopes where child scopes are automatically disposed when the parent is disposed.
/// </summary>
public sealed class SvcScope : ISvcScope
{
    private readonly FrozenDictionary<Type, SvcDescriptor[]>? _descriptorCache;
    private readonly ConcurrentDictionary<Type, SvcDescriptor[]>? _concurrentDescriptorCache;

    // Use lazy initialization to avoid allocation overhead when scopes are short-lived
    private ConcurrentDictionary<SvcDescriptor, object>? _scopedInstances;
    private ConcurrentBag<SvcScope>? _childScopes;

    private static readonly ConcurrentDictionary<SvcDescriptor, object> SingletonLocks = new();
    private int _disposed; // 0 = not disposed, 1 = disposed (for thread-safe Interlocked operations)

    // Lazy property accessors to delay allocation
    private ConcurrentDictionary<SvcDescriptor, object> ScopedInstances =>
        _scopedInstances ??= new ConcurrentDictionary<SvcDescriptor, object>();

    private ConcurrentBag<SvcScope> ChildScopes => _childScopes ??= new ConcurrentBag<SvcScope>();

    /// <summary>
    /// Initializes a new instance of <see cref="SvcScope"/> with a frozen (optimized) descriptor cache.
    /// </summary>
    /// <param name="descriptorCache">The frozen dictionary containing service descriptors.</param>
    public SvcScope(FrozenDictionary<Type, SvcDescriptor[]> descriptorCache)
    {
        _descriptorCache = descriptorCache;
        _concurrentDescriptorCache = null;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="SvcScope"/> with a concurrent descriptor cache.
    /// Used when the container has not been built yet.
    /// </summary>
    /// <param name="descriptorCache">The concurrent dictionary containing service descriptors.</param>
    public SvcScope(ConcurrentDictionary<Type, SvcDescriptor[]> descriptorCache)
    {
        _concurrentDescriptorCache = descriptorCache;
        _descriptorCache = null;
    }

    /// <inheritdoc />
    public ISvcScope CreateScope()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

        // Create child scope and track it for automatic disposal when parent is disposed
        var childScope =
            _descriptorCache != null
                ? new SvcScope(_descriptorCache)
                : new SvcScope(_concurrentDescriptorCache!);

        ChildScopes.Add(childScope);
        return childScope;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object GetService(Type serviceType)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

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

    /// <inheritdoc />
    public IEnumerable<object> GetServices(Type serviceType)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
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
        // Fast path: check if already created (most common case after warmup)
        var instance = resolver.SingleInstance;
        if (instance != null)
            return instance;

        // Slow path: use lock to ensure single creation
        return GetOrCreateSingletonSlow(serviceType, resolver);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private object GetOrCreateSingletonSlow(Type serviceType, SvcDescriptor resolver)
    {
        var lockObj = SingletonLocks.GetOrAdd(resolver, static _ => new object());
        lock (lockObj)
        {
            // Double-check after acquiring lock
            var instance = resolver.SingleInstance;
            if (instance != null)
                return instance;

            instance =
                resolver.Factory != null
                    ? resolver.Factory(this)
                    : throw new PicoDiException(
                        $"No factory or instance registered for singleton service '{serviceType.FullName}'. "
                            + "Use Pico.DI.Gen source generator or register with a factory delegate."
                    );

            resolver.SingleInstance = instance;
            return instance;
        }
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
        ScopedInstances.GetOrAdd(
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

    /// <inheritdoc />
    public void Dispose()
    {
        // Thread-safe check-and-set using Interlocked
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        // Dispose child scopes first (depth-first disposal)
        if (_childScopes != null)
        {
            while (_childScopes.TryTake(out var childScope))
            {
                childScope.Dispose();
            }
        }

        // Then dispose scoped instances owned by this scope
        if (_scopedInstances != null)
        {
            foreach (var svc in _scopedInstances.Values)
            {
                if (svc is IDisposable disposable)
                    disposable.Dispose();
            }
            _scopedInstances.Clear();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Thread-safe check-and-set using Interlocked
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        // Dispose child scopes first (depth-first disposal)
        if (_childScopes != null)
        {
            while (_childScopes.TryTake(out var childScope))
            {
                await childScope.DisposeAsync();
            }
        }

        // Then dispose scoped instances owned by this scope
        if (_scopedInstances != null)
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
}
