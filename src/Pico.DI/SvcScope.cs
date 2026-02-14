namespace Pico.DI;

using System.Diagnostics;

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

    // Eagerly initialized lock for scoped instance cache.
    // Must NOT use lazy `field ??=` pattern — it is non-atomic and can cause two threads
    // to obtain different lock objects, breaking mutual exclusion on the Dictionary.
    private readonly Lock _scopedLock = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            ThrowObjectDisposedException();
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowObjectDisposedException() =>
        throw new ObjectDisposedException(nameof(SvcScope));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogDisposeError(object? instance, Exception exception)
    {
        // Use Trace for AOT-compatible logging
        // Users can configure Trace listeners if they need to capture these errors
        Trace.WriteLine($"Error disposing scoped service instance of type '{instance?.GetType().FullName ?? "unknown"}': {exception}");
    }

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
            // Use Volatile.Read to ensure cross-thread visibility (critical on ARM architectures)
            var instance = Volatile.Read(ref singletonDescriptor.SingleInstance);
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
            // Defensive: handle Singleton here in case it wasn't in the fast singleton cache
            // (e.g., if the type has mixed-lifetime registrations where the last is not singleton)
            SvcLifetime.Singleton
                => GetOrCreateSingleton(serviceType, resolver),
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
        if (!_descriptorCache.TryGetValue(serviceType, out var resolvers))
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

    /// <summary>
    /// Resolves or creates a scoped service instance.
    /// All access to the scoped instance dictionary is synchronized to prevent data corruption
    /// from concurrent reads/writes on the non-thread-safe Dictionary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private object GetOrAddScopedInstance(SvcDescriptor resolver)
    {
        lock (_scopedLock)
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
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        DisposeChildScopes();
        DisposeScopedInstances();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        await DisposeChildScopesAsync();
        await DisposeScopedInstancesAsync();
    }

    /// <summary>
    /// Atomically seals the child scope list and synchronously disposes all child scopes.
    /// </summary>
    private void DisposeChildScopes()
    {
        var child = Interlocked.Exchange(ref _firstChildScope, DisposedSentinel);
        while (child != null && !ReferenceEquals(child, DisposedSentinel))
        {
            var next = child.NextInList;
            try
            {
                child.Dispose();
            }
            catch (Exception ex)
            {
                // Log the exception but continue disposing other child scopes
                // In AOT environments, we prioritize completing cleanup over error reporting
                Trace.WriteLine($"Error disposing child scope: {ex}");
            }
            child = next;
        }
    }

    /// <summary>
    /// Atomically seals the child scope list and asynchronously disposes all child scopes.
    /// </summary>
    private async ValueTask DisposeChildScopesAsync()
    {
        var child = Interlocked.Exchange(ref _firstChildScope, DisposedSentinel);
        while (child != null && !ReferenceEquals(child, DisposedSentinel))
        {
            var next = child.NextInList;
            try
            {
                await child.DisposeAsync();
            }
            catch (Exception ex)
            {
                // Log the exception but continue disposing other child scopes
                // In AOT environments, we prioritize completing cleanup over error reporting
                Trace.WriteLine($"Error disposing child scope asynchronously: {ex}");
            }
            child = next;
        }
    }

    /// <summary>
    /// Synchronously disposes all scoped instances owned by this scope.
    /// </summary>
    private void DisposeScopedInstances()
    {
        var instances = Volatile.Read(ref _scopedInstances);
        if (instances == null)
            return;

        foreach (var svc in instances.Values)
        {
            try
            {
                switch (svc)
                {
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                    // Handle objects that only implement IAsyncDisposable but not IDisposable.
                    case IAsyncDisposable asyncDisposable:
                        asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
                        break;
                }
            }
            catch (Exception ex)
            {
                // Log the exception but continue disposing other instances
                // In AOT environments, we prioritize completing cleanup over error reporting
                LogDisposeError(svc, ex);
            }
        }

        instances.Clear();
    }

    /// <summary>
    /// Asynchronously disposes all scoped instances owned by this scope.
    /// </summary>
    private async ValueTask DisposeScopedInstancesAsync()
    {
        var instances = Volatile.Read(ref _scopedInstances);
        if (instances == null)
            return;

        foreach (var svc in instances.Values)
        {
            try
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
            catch (Exception ex)
            {
                // Log the exception but continue disposing other instances
                // In AOT environments, we prioritize completing cleanup over error reporting
                LogDisposeError(svc, ex);
            }
        }

        instances.Clear();
    }
}
