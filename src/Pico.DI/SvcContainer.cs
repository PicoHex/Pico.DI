namespace Pico.DI;

/// <summary>
/// A high-performance, AOT-compatible dependency injection container.
/// Manages service registrations, scope creation, and singleton instance lifecycle.
/// </summary>
public sealed class SvcContainer : ISvcContainer
{
    private readonly ConcurrentDictionary<Type, SvcDescriptor[]> _descriptorCache = new();

    // Use simple linked list for root scopes - lower overhead than ConcurrentBag
    private SvcScope? _firstRootScope;
    private object? _rootScopeLock;
    private object RootScopeLock => _rootScopeLock ??= new object();

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
    private void ThrowObjectDisposedException() =>
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

        if (_frozenCache != null)
            throw new InvalidOperationException(
                "Cannot register services after Build() has been called. "
                    + "Register all services before calling Build()."
            );

        // This method is called by source-generated ConfigureGeneratedServices() method
        // with pre-built SvcDescriptor instances that already contain compiled factory delegates.
        // We simply cache them - no reflection, no factory generation at runtime.
        _descriptorCache.AddOrUpdate(
            descriptor.ServiceType,
            _ => [descriptor],
            (_, existing) => [.. existing, descriptor]
        );

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
    public SvcContainer Build()
    {
        ThrowIfDisposed();

        if (_frozenCache != null)
            return this;

        _frozenCache = _descriptorCache.ToFrozenDictionary();
        return this;
    }

    /// <inheritdoc />
    public ISvcScope CreateScope()
    {
        ThrowIfDisposed();

        // Create scope - use frozen cache after Build() for maximum performance
        var frozenCache = _frozenCache;
        var scope =
            frozenCache != null ? new SvcScope(frozenCache) : new SvcScope(_descriptorCache);

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
        foreach (var keyValuePair in _descriptorCache)
        {
            foreach (var svc in keyValuePair.Value)
            {
                (svc.SingleInstance as IDisposable)?.Dispose();
            }
        }
        _descriptorCache.Clear();
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
        foreach (var keyValuePair in _descriptorCache)
        {
            foreach (var svc in keyValuePair.Value)
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
        _descriptorCache.Clear();
    }
}
