namespace Pico.DI;

/// <summary>
/// Represents a service scope that manages the lifetime of scoped service instances.
/// Supports hierarchical scopes where child scopes are automatically disposed when the parent is disposed.
/// </summary>
public sealed class SvcScope : ISvcScope
{
    private readonly FrozenDictionary<Type, SvcDescriptor[]>? _descriptorCache;
    private readonly ConcurrentDictionary<Type, SvcDescriptor[]>? _concurrentDescriptorCache;

    // Scoped instances: use Dictionary + lock for better single-thread performance (most common case)
    private Dictionary<SvcDescriptor, object>? _scopedInstances;

    // Child scopes linked list - lower overhead than ConcurrentBag
    private SvcScope? _firstChild;

    // For container's root scope tracking (linked list)
    internal SvcScope? NextSibling;

    private int _disposed; // 0 = not disposed, 1 = disposed (for thread-safe Interlocked operations)

    // Lazy accessors
    private object ScopedLock => field ??= new object();
    private object ChildLock => field ??= new object();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed != 0)
            ThrowObjectDisposedException();
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowObjectDisposedException() =>
        throw new ObjectDisposedException(nameof(SvcScope));

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
        ThrowIfDisposed();

        // Create child scope
        var childScope =
            _descriptorCache != null
                ? new SvcScope(_descriptorCache)
                : new SvcScope(_concurrentDescriptorCache!);

        // Track it for automatic disposal using linked list
        lock (ChildLock)
        {
            childScope.NextSibling = _firstChild;
            _firstChild = childScope;
        }
        return childScope;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object GetService(Type serviceType)
    {
        ThrowIfDisposed();

        // Fast path: inline frozen cache lookup (most common after Build())
        SvcDescriptor[]? resolvers;
        if (_descriptorCache != null)
        {
            if (!_descriptorCache.TryGetValue(serviceType, out resolvers))
                return HandleServiceNotFound(serviceType);
        }
        else if (!_concurrentDescriptorCache!.TryGetValue(serviceType, out resolvers))
        {
            return HandleServiceNotFound(serviceType);
        }

        // Get last registered descriptor (override pattern)
        var resolver = resolvers[^1];

        // Inline lifetime dispatch for hot path
        return resolver.Lifetime switch
        {
            SvcLifetime.Singleton => GetOrCreateSingleton(serviceType, resolver),
            SvcLifetime.Transient => ResolveTransient(serviceType, resolver),
            SvcLifetime.Scoped => GetOrAddScopedInstance(resolver),
            _ => ThrowUnknownLifetime(resolver.Lifetime)
        };
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private object HandleServiceNotFound(Type serviceType)
    {
        // Check for open generic
        if (!serviceType.IsGenericType)
            throw new PicoDiException($"Service type '{serviceType.FullName}' is not registered.");
        var openGenericType = serviceType.GetGenericTypeDefinition();
        if (
            (_descriptorCache?.ContainsKey(openGenericType) ?? false)
            || (_concurrentDescriptorCache?.ContainsKey(openGenericType) ?? false)
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

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object ThrowUnknownLifetime(SvcLifetime lifetime) =>
        throw new ArgumentOutOfRangeException(
            nameof(SvcLifetime),
            lifetime,
            $"Unknown service lifetime '{lifetime}'."
        )
        {
            HelpLink = null,
            HResult = 0,
            Source = null
        };

    /// <inheritdoc />
    public IEnumerable<object> GetServices(Type serviceType)
    {
        ThrowIfDisposed();
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
        return instance
            ??
            // Slow path: use lock to ensure single creation
            GetOrCreateSingletonSlow(serviceType, resolver);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private object GetOrCreateSingletonSlow(Type serviceType, SvcDescriptor resolver)
    {
        // Use the descriptor itself as lock object (safe since SvcDescriptor is a reference type)
        lock (resolver)
        {
            // Double-check after acquiring lock
            var instance = Volatile.Read(ref resolver.SingleInstance);
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
    private object GetOrAddScopedInstance(SvcDescriptor resolver)
    {
        // Fast path: check without lock (most common case after first resolution)
        var instances = _scopedInstances;
        if (instances != null && instances.TryGetValue(resolver, out var existing))
            return existing;

        return GetOrAddScopedInstanceSlow(resolver);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private object GetOrAddScopedInstanceSlow(SvcDescriptor resolver)
    {
        lock (ScopedLock)
        {
            _scopedInstances ??= new Dictionary<SvcDescriptor, object>();

            if (_scopedInstances.TryGetValue(resolver, out var existing))
                return existing;

            var instance =
                resolver.Factory != null
                    ? resolver.Factory(this)
                    : throw new PicoDiException(
                        $"No factory registered for scoped service '{resolver.ServiceType.FullName}'. "
                            + "Use Pico.DI.Gen source generator or register with a factory delegate."
                    );

            _scopedInstances[resolver] = instance;
            return instance;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Thread-safe check-and-set using Interlocked
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        // Dispose child scopes first (depth-first disposal) using linked list
        var child = _firstChild;
        while (child != null)
        {
            var next = child.NextSibling;
            child.Dispose();
            child = next;
        }
        _firstChild = null;

        // Then dispose scoped instances owned by this scope
        var instances = _scopedInstances;
        if (instances == null)
            return;
        foreach (var svc in instances.Values)
        {
            (svc as IDisposable)?.Dispose();
        }
        instances.Clear();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Thread-safe check-and-set using Interlocked
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        // Dispose child scopes first (depth-first disposal) using linked list
        var child = _firstChild;
        while (child != null)
        {
            var next = child.NextSibling;
            await child.DisposeAsync();
            child = next;
        }
        _firstChild = null;

        // Then dispose scoped instances owned by this scope
        var instances = _scopedInstances;
        if (instances != null)
        {
            foreach (var svc in instances.Values)
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
            instances.Clear();
        }
    }
}
