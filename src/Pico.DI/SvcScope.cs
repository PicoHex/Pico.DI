namespace Pico.DI;

/// <summary>
/// Represents a service scope that manages the lifetime of scoped service instances.
/// Supports hierarchical scopes where child scopes are automatically disposed when the parent is disposed.
/// </summary>
public sealed class SvcScope : ISvcScope
{
    private readonly FrozenDictionary<Type, SvcDescriptor[]> _descriptorCache;

    /// <summary>
    /// Fast singleton cache: direct Type -> SvcDescriptor lookup for singleton services.
    /// Avoids array indexing overhead in the hot path.
    /// </summary>
    private readonly FrozenDictionary<Type, SvcDescriptor> _singletonCache;

    // Scoped instances: use Dictionary + lock for better single-thread performance (most common case)
    private Dictionary<SvcDescriptor, object>? _scopedInstances;

    // Sentinel object to mark the linked list as "disposed" - prevents new child scopes from being added
    private static readonly SvcScope DisposedSentinel = CreateSentinel();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static SvcScope CreateSentinel() => new(null!, null!);

    // Child scopes linked list - lock-free prepend using CAS
    private SvcScope? _firstChildScope;

    // Linked list next pointer.
    // Used for: (1) child scopes under a parent scope, and (2) container root scope tracking.
    internal SvcScope? NextInList;

    private int _disposed; // 0 = not disposed, 1 = disposed (for thread-safe Interlocked operations)

    // Lazy accessor for scoped instances lock (still needed for scoped instance cache)
    private Lock ScopedLock => field ??= new Lock();

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
    /// <param name="singletonCache">The frozen dictionary for fast singleton lookup.</param>
    public SvcScope(
        FrozenDictionary<Type, SvcDescriptor[]> descriptorCache,
        FrozenDictionary<Type, SvcDescriptor> singletonCache
    )
    {
        _descriptorCache = descriptorCache;
        _singletonCache = singletonCache;
    }

    /// <inheritdoc />
    public ISvcScope CreateScope()
    {
        ThrowIfDisposed();

        // Create child scope with same caches
        var childScope = new SvcScope(_descriptorCache, _singletonCache);

        // Lock-free prepend using CAS with sentinel check for 100% disposal safety
        SvcScope? current;
        do
        {
            current = Volatile.Read(ref _firstChildScope);
            if (ReferenceEquals(current, DisposedSentinel))
                ThrowObjectDisposedException(); // Parent scope is disposing, reject new child scopes
            childScope.NextInList = current;
        } while (Interlocked.CompareExchange(ref _firstChildScope, childScope, current) != current);

        return childScope;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object GetService(Type serviceType)
    {
        ThrowIfDisposed();

        // Ultra-fast path for singletons: direct lookup without array indexing
        if (_singletonCache.TryGetValue(serviceType, out var singletonDescriptor))
        {
            // Fast path: singleton already initialized (most common case after warmup)
            var instance = singletonDescriptor.SingleInstance;
            if (instance != null)
                return instance;

            // Slow path: initialize singleton
            return GetOrCreateSingletonSlow(serviceType, singletonDescriptor);
        }

        // Standard path for non-singleton services
        if (!_descriptorCache.TryGetValue(serviceType, out var resolvers))
            return HandleServiceNotFound(serviceType);

        // Get last registered descriptor (override pattern)
        var resolver = resolvers[^1];

        // Inline lifetime dispatch for hot path
        return resolver.Lifetime switch
        {
            SvcLifetime.Transient => ResolveTransient(serviceType, resolver),
            SvcLifetime.Scoped => GetOrAddScopedInstance(resolver),
            _ => ThrowUnknownLifetime(resolver.Lifetime)
        };
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [DoesNotReturn]
    private static object HandleServiceNotFound(Type serviceType)
    {
        // At compile time, the source generator should have detected all needed
        // closed generic types and generated registrations for them.
        // If we reach here, it means a service was requested that wasn't registered
        // and wasn't auto-discovered at compile time.
        throw new PicoDiException(
            $"Service type '{serviceType.FullName}' is not registered. "
                + "Ensure the service is registered explicitly or that the source generator "
                + "can discover its implementation from referenced assemblies."
        );
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object ThrowUnknownLifetime(SvcLifetime lifetime) =>
        throw new ArgumentOutOfRangeException(
            nameof(SvcLifetime),
            lifetime,
            $"Unknown service lifetime '{lifetime}'."
        );

    /// <inheritdoc />
    public IEnumerable<object> GetServices(Type serviceType)
    {
        ThrowIfDisposed();
        if (!TryGetResolvers(serviceType, out var resolvers))
            throw new PicoDiException($"Service type '{serviceType.FullName}' is not registered.");

        return resolvers!
            .Select(resolver =>
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
        // Fast path: Volatile.Read ensures visibility of writes from other threads
        // and prevents JIT from caching the field read across iterations
        return Volatile.Read(ref resolver.SingleInstance)
            ?? GetOrCreateSingletonSlow(serviceType, resolver);
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
        if (_descriptorCache.TryGetValue(serviceType, out resolvers))
            return true;
        resolvers = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private object GetOrAddScopedInstance(SvcDescriptor resolver)
    {
        // Fast path: direct dictionary lookup (no null check - initialized on first slow path)
        var instances = _scopedInstances;
        if (instances != null)
        {
            // Use TryGetValue for fastest lookup
            if (instances.TryGetValue(resolver, out var existing))
                return existing;
        }

        return GetOrAddScopedInstanceSlow(resolver);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private object GetOrAddScopedInstanceSlow(SvcDescriptor resolver)
    {
        lock (ScopedLock)
        {
            _scopedInstances ??= new Dictionary<SvcDescriptor, object>();

            // Use CollectionsMarshal to avoid double dictionary lookup
            ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(
                _scopedInstances,
                resolver,
                out bool exists
            );

            if (exists)
                return slot!;

            var instance =
                resolver.Factory != null
                    ? resolver.Factory(this)
                    : throw new PicoDiException(
                        $"No factory registered for scoped service '{resolver.ServiceType.FullName}'. "
                            + "Use Pico.DI.Gen source generator or register with a factory delegate."
                    );

            slot = instance;
            return instance;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Thread-safe check-and-set using Interlocked
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        // Atomically acquire the child scope list and set sentinel to block new child scope creation
        var head = Interlocked.Exchange(ref _firstChildScope, DisposedSentinel);

        // Dispose child scopes first (depth-first disposal)
        var child = head;
        while (child != null && !ReferenceEquals(child, DisposedSentinel))
        {
            var next = child.NextInList;
            child.Dispose();
            child = next;
        }

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

        // Atomically acquire the child scope list and set sentinel to block new child scope creation
        var head = Interlocked.Exchange(ref _firstChildScope, DisposedSentinel);

        // Dispose child scopes first (depth-first disposal)
        var child = head;
        while (child != null && !ReferenceEquals(child, DisposedSentinel))
        {
            var next = child.NextInList;
            await child.DisposeAsync();
            child = next;
        }

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
