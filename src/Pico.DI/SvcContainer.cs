namespace Pico.DI;

/// <summary>
/// A high-performance, AOT-compatible dependency injection container.
/// Manages service registrations, scope creation, and singleton instance lifecycle.
/// </summary>
public sealed class SvcContainer : ISvcContainer
{
    private Dictionary<Type, SvcDescriptor[]>? _descriptorCache = new();
    private object? _registrationLock;
    private object RegistrationLock => _registrationLock ??= new object();

    // Sentinel object to mark the linked list as "disposed" - prevents new scopes from being added
    // Using a special marker value instead of null to distinguish "empty list" from "disposed list"
    private static readonly SvcScope DisposedSentinel = CreateSentinel();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static SvcScope CreateSentinel() => new(null!);

    // Use simple linked list for root scopes - lower overhead than ConcurrentBag
    // Lock-free prepend using CAS; disposal uses Exchange to atomically acquire and seal the list
    private SvcScope? _firstRootScope;

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

        lock (RegistrationLock)
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
                cache[descriptor.ServiceType] = new[] { descriptor };
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

        lock (RegistrationLock)
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
        // Thread-safe check-and-set using Interlocked
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        // Atomically acquire the linked list and set sentinel to block new scope creation
        // This guarantees no scope can be added after this point (they'll see DisposedSentinel and throw)
        var head = Interlocked.Exchange(ref _firstRootScope, DisposedSentinel);

        // Dispose all root scopes (which will recursively dispose their child scopes)
        var scope = head;
        while (scope != null && !ReferenceEquals(scope, DisposedSentinel))
        {
            var next = scope.NextInList;
            scope.Dispose();
            scope = next;
        }

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

        // Atomically acquire the linked list and set sentinel to block new scope creation
        var head = Interlocked.Exchange(ref _firstRootScope, DisposedSentinel);

        // Dispose all root scopes (which will recursively dispose their child scopes)
        var scope = head;
        while (scope != null && !ReferenceEquals(scope, DisposedSentinel))
        {
            var next = scope.NextInList;
            await scope.DisposeAsync();
            scope = next;
        }

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
