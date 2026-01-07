namespace Pico.DI;

/// <summary>
/// A high-performance, AOT-compatible dependency injection container.
/// Manages service registrations, scope creation, and singleton instance lifecycle.
/// </summary>
public sealed class SvcContainer : ISvcContainer
{
    private Dictionary<Type, SvcDescriptor[]>? _descriptorCache = new();
    private object SyncRoot => field ??= new object();

    // Use simple linked list for root scopes - lower overhead than ConcurrentBag
    private SvcScope? _firstRootScope;
    private object RootScopeLock => field ??= new object();

    /// <summary>
    /// Frozen (optimized) descriptor cache after Build() is called.
    /// </summary>
    private FrozenDictionary<Type, SvcDescriptor[]>? _frozenCache;

    private int _disposed; // 0 = not disposed, 1 = disposed (for thread-safe Interlocked operations)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed != 0)
            ThrowObjectDisposedException();
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowObjectDisposedException() =>
        throw new ObjectDisposedException(nameof(SvcContainer));

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

        lock (SyncRoot)
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
                updated[^1] = descriptor;
                cache[descriptor.ServiceType] = updated;
            }
            else
            {
                cache[descriptor.ServiceType] = [descriptor];
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

        lock (SyncRoot)
        {
            if (_frozenCache != null)
                return;

            var cache = _descriptorCache ?? new Dictionary<Type, SvcDescriptor[]>();
            _frozenCache = cache.ToFrozenDictionary();

            // After build, registrations are immutable and resolution uses frozen cache.
            // Drop the registration cache to reduce memory overhead.
            _descriptorCache = null;
        }
    }

    /// <inheritdoc />
    public ISvcScope CreateScope()
    {
        ThrowIfDisposed();

        // Ensure the container is built before creating scopes/resolving services.
        // This keeps resolution on FrozenDictionary hot path, while avoiding requiring callers to remember Build().
        var frozenCache = _frozenCache;
        if (frozenCache == null)
        {
            Build();
            frozenCache = _frozenCache!;
        }

        var scope = new SvcScope(frozenCache);

        // Track for automatic disposal using simple linked list
        lock (RootScopeLock)
        {
            scope.NextSibling = _firstRootScope;
            _firstRootScope = scope;
        }
        return scope;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Thread-safe check-and-set using Interlocked
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        // Dispose all root scopes first (which will recursively dispose their child scopes)
        var scope = _firstRootScope;
        while (scope != null)
        {
            var next = scope.NextSibling;
            scope.Dispose();
            scope = next;
        }
        _firstRootScope = null;

        // Then dispose singleton instances owned by the container
        var frozenCache = _frozenCache;
        if (frozenCache != null)
        {
            foreach (var svc in frozenCache.SelectMany(keyValuePair => keyValuePair.Value))
            {
                (svc.SingleInstance as IDisposable)?.Dispose();
            }
        }
        else
        {
            var cache = _descriptorCache;
            if (cache != null)
            {
                foreach (var svc in cache.SelectMany(keyValuePair => keyValuePair.Value))
                {
                    (svc.SingleInstance as IDisposable)?.Dispose();
                }

                cache.Clear();
            }
        }

        _descriptorCache = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Thread-safe check-and-set using Interlocked
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        // Dispose all root scopes first (which will recursively dispose their child scopes)
        var scope = _firstRootScope;
        while (scope != null)
        {
            var next = scope.NextSibling;
            await scope.DisposeAsync();
            scope = next;
        }
        _firstRootScope = null;

        // Then dispose singleton instances owned by the container
        var frozenCache = _frozenCache;
        if (frozenCache != null)
        {
            foreach (var svc in frozenCache.SelectMany(keyValuePair => keyValuePair.Value))
            {
                switch (svc.SingleInstance)
                {
                    case IAsyncDisposable asyncDisposable:
                        await asyncDisposable.DisposeAsync();
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
            }
        }
        else
        {
            var cache = _descriptorCache;
            if (cache != null)
            {
                foreach (var svc in cache.SelectMany(keyValuePair => keyValuePair.Value))
                {
                    switch (svc.SingleInstance)
                    {
                        case IAsyncDisposable asyncDisposable:
                            await asyncDisposable.DisposeAsync();
                            break;
                        case IDisposable disposable:
                            disposable.Dispose();
                            break;
                    }
                }

                cache.Clear();
            }
        }

        _descriptorCache = null;
    }
}
