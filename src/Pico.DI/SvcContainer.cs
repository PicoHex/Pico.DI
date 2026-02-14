namespace Pico.DI;

using System.Diagnostics;

/// <summary>
/// A high-performance, AOT-compatible dependency injection container.
/// Manages service registrations, scope creation, and singleton instance lifecycle.
/// </summary>
public sealed class SvcContainer : ISvcContainer
{
    private Dictionary<Type, SvcDescriptor[]>? _descriptorCache = new();

    // Eagerly initialized lock for registration/build operations.
    // Must NOT use lazy `field ??=` pattern — it is non-atomic and can cause two threads
    // to obtain different lock objects, breaking mutual exclusion.
    private readonly Lock _registrationLock = new();

    // Sentinel object to mark the linked list as "disposed" - prevents new scopes from being added
    // Using a special marker value instead of null to distinguish "empty list" from "disposed list"
    private static readonly SvcScope DisposedSentinel = CreateSentinel();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static SvcScope CreateSentinel() => new(null!, null!);

    // Use simple linked list for root scopes - lower overhead than ConcurrentBag
    // Lock-free prepend using CAS; disposal uses Exchange to atomically acquire and seal the list
    private SvcScope? _firstRootScope;

    /// <summary>
    /// Frozen (optimized) descriptor cache after Build() is called.
    /// </summary>
    private FrozenDictionary<Type, SvcDescriptor[]>? _frozenCache;

    /// <summary>
    /// Fast singleton cache: Type -> SvcDescriptor (for singleton services only).
    /// This provides O(1) lookup without going through the full descriptor array.
    /// </summary>
    private FrozenDictionary<Type, SvcDescriptor>? _singletonCache;

    private int _disposed; // 0 = not disposed, 1 = disposed (for thread-safe Interlocked operations)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            ThrowObjectDisposedException();
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowObjectDisposedException() =>
        throw new ObjectDisposedException(nameof(SvcContainer));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogDisposeError(object? instance, Exception exception)
    {
        // Use Trace for AOT-compatible logging
        // Users can configure Trace listeners if they need to capture these errors
        Trace.WriteLine($"Error disposing service instance of type '{instance?.GetType().FullName ?? "unknown"}': {exception}");
    }

    /// <summary>
    /// Creates a new instance of <see cref="SvcContainer"/>.
    /// If a source-generated configurator has been registered via Module Initializer,
    /// it will be automatically applied to this container.
    /// </summary>
    /// <param name="autoConfigureFromGenerator">
    /// If true (default), automatically applies source-generated service registrations.
    /// Set to false if you want to manually configure the container without auto-generated services.
    /// </param>
    public SvcContainer(bool autoConfigureFromGenerator = true)
    {
        if (autoConfigureFromGenerator)
        {
            SvcContainerAutoConfiguration.TryApplyConfiguration(this);
        }
    }

    /// <inheritdoc />
    public ISvcContainer Register(SvcDescriptor descriptor)
    {
        ThrowIfDisposed();

        lock (_registrationLock)
        {
            if (_frozenCache != null)
                throw new InvalidOperationException(
                    "Cannot register services after Build() has been called. "
                        + "Register all services before calling Build()."
                );

            // This method is called by source-generated ConfigureGeneratedServices() method
            // with pre-built SvcDescriptor instances that already contain compiled factory delegates.
            // We simply cache them - no reflection, no factory generation at runtime.
            var cache =
                _descriptorCache
                ?? throw new InvalidOperationException(
                    "Registration cache is not available. Ensure services are registered before Build()."
                );

            if (cache.TryGetValue(descriptor.ServiceType, out var existing))
            {
                var updated = new SvcDescriptor[existing.Length + 1];
                Array.Copy(existing, updated, existing.Length);
                updated[existing.Length] = descriptor;
                cache[descriptor.ServiceType] = updated;
            }
            else
            {
                cache[descriptor.ServiceType] =  [descriptor];
            }
        }

        return this;
    }

    /// <summary>
    /// Builds and optimizes the container for maximum performance.
    /// Converts internal ConcurrentDictionary to FrozenDictionary for fastest lookups.
    /// Call this method after all services have been registered.
    /// After calling Build(), no more services can be registered.
    ///
    /// NOTE: This method is automatically called by the source-generated ConfigureGeneratedServices() method.
    /// You do not need to call it manually unless you are registering services without using the source generator.
    /// </summary>
    /// <returns>The container instance for method chaining.</returns>
    public void Build()
    {
        ThrowIfDisposed();

        lock (_registrationLock)
        {
            if (_frozenCache != null)
                return;

            var cache = _descriptorCache ?? new Dictionary<Type, SvcDescriptor[]>();
            Volatile.Write(ref _frozenCache, cache.ToFrozenDictionary());

            // Build singleton cache for fast singleton lookup
            // Only include types where the last registered service is a singleton
            var singletonDict = new Dictionary<Type, SvcDescriptor>();
            foreach (var kvp in cache)
            {
                var lastDescriptor = kvp.Value[^1];
                if (lastDescriptor.Lifetime == SvcLifetime.Singleton)
                {
                    singletonDict[kvp.Key] = lastDescriptor;
                }
            }
            Volatile.Write(ref _singletonCache, singletonDict.ToFrozenDictionary());

            // After build, registrations are immutable and resolution uses frozen cache.
            // Drop the registration cache to reduce memory overhead.
            Volatile.Write(ref _descriptorCache, null);
        }
    }

    /// <inheritdoc />
    public ISvcScope CreateScope()
    {
        ThrowIfDisposed();

        // Ensure the container is built before creating scopes/resolving services.
        // This keeps resolution on FrozenDictionary hot path, while avoiding requiring callers to remember Build().
        var frozenCache = Volatile.Read(ref _frozenCache);
        var singletonCache = Volatile.Read(ref _singletonCache);
        if (frozenCache == null || singletonCache == null)
        {
            Build();
            frozenCache = Volatile.Read(ref _frozenCache)!;
            singletonCache = Volatile.Read(ref _singletonCache)!;
        }

        var scope = new SvcScope(frozenCache, singletonCache);

        // Lock-free prepend using CAS with sentinel check for 100% disposal safety
        SvcScope? current;
        do
        {
            current = Volatile.Read(ref _firstRootScope);
            if (ReferenceEquals(current, DisposedSentinel))
                ThrowObjectDisposedException(); // Container is disposing, reject new scopes
            scope.NextInList = current;
        } while (Interlocked.CompareExchange(ref _firstRootScope, scope, current) != current);

        return scope;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        DisposeRootScopes();
        DisposeSingletonInstances();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        await DisposeRootScopesAsync();
        await DisposeSingletonInstancesAsync();
    }

    /// <summary>
    /// Atomically seals the root scope list and synchronously disposes all root scopes.
    /// </summary>
    private void DisposeRootScopes()
    {
        var scope = Interlocked.Exchange(ref _firstRootScope, DisposedSentinel);
        while (scope != null && !ReferenceEquals(scope, DisposedSentinel))
        {
            var next = scope.NextInList;
            try
            {
                scope.Dispose();
            }
            catch (Exception ex)
            {
                // Log the exception but continue disposing other scopes
                // In AOT environments, we prioritize completing cleanup over error reporting
                Trace.WriteLine($"Error disposing root scope: {ex}");
            }
            scope = next;
        }
    }

    /// <summary>
    /// Atomically seals the root scope list and asynchronously disposes all root scopes.
    /// </summary>
    private async ValueTask DisposeRootScopesAsync()
    {
        var scope = Interlocked.Exchange(ref _firstRootScope, DisposedSentinel);
        while (scope != null && !ReferenceEquals(scope, DisposedSentinel))
        {
            var next = scope.NextInList;
            try
            {
                await scope.DisposeAsync();
            }
            catch (Exception ex)
            {
                // Log the exception but continue disposing other scopes
                // In AOT environments, we prioritize completing cleanup over error reporting
                Trace.WriteLine($"Error disposing root scope asynchronously: {ex}");
            }
            scope = next;
        }
    }

    /// <summary>
    /// Enumerates all descriptors from either the frozen cache or the mutable registration cache.
    /// </summary>
    private IEnumerable<SvcDescriptor> EnumerateAllDescriptors()
    {
        var frozen = Volatile.Read(ref _frozenCache);
        if (frozen != null)
            return frozen.SelectMany(kvp => kvp.Value);

        var cache = Volatile.Read(ref _descriptorCache);
        return cache != null ? cache.SelectMany(kvp => kvp.Value) : [];
    }

    /// <summary>
    /// Synchronously disposes all singleton instances and clears the registration cache.
    /// </summary>
    private void DisposeSingletonInstances()
    {
        var disposedInstances = new HashSet<object>();
        
        foreach (var svc in EnumerateAllDescriptors())
        {
            var instance = Volatile.Read(ref svc.SingleInstance);
            if (instance == null)
                continue;
                
            // Skip if already disposed
            if (!disposedInstances.Add(instance))
                continue;
                
            try
            {
                switch (instance)
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
                LogDisposeError(instance, ex);
            }
        }

        var cache = Volatile.Read(ref _descriptorCache);
        cache?.Clear();
        Volatile.Write(ref _descriptorCache, null);
    }

    /// <summary>
    /// Asynchronously disposes all singleton instances and clears the registration cache.
    /// </summary>
    private async ValueTask DisposeSingletonInstancesAsync()
    {
        var disposedInstances = new HashSet<object>();
        
        foreach (var svc in EnumerateAllDescriptors())
        {
            var instance = Volatile.Read(ref svc.SingleInstance);
            if (instance == null)
                continue;
                
            // Skip if already disposed
            if (!disposedInstances.Add(instance))
                continue;
                
            try
            {
                switch (instance)
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
                LogDisposeError(instance, ex);
            }
        }

        var cache = Volatile.Read(ref _descriptorCache);
        cache?.Clear();
        Volatile.Write(ref _descriptorCache, null);
    }
}
