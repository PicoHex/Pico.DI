namespace Pico.DI;

/// <summary>
/// The main dependency injection container for registering and managing service descriptors.
/// Implements <see cref="ISvcContainer"/> and supports both synchronous and asynchronous disposal.
///
/// ARCHITECTURE: Zero-Reflection Compile-Time Factory Generation
/// ===============================================================
///
/// This container uses a DUAL-MODE architecture:
///
/// PRODUCTION PATH (with Source Generator):
/// =========================================
/// 1. Source generator scans RegisterSingleton<T, TImpl>() calls at compile-time
/// 2. Generator analyzes constructor parameters and generates explicit factory code
/// 3. Generator produces ConfigureGeneratedServices() with pre-built SvcDescriptor instances
/// 4. Each descriptor contains pre-compiled factory (e.g., static _ => new TImpl())
/// 5. At runtime: Register(SvcDescriptor) simply caches these pre-built descriptors
/// 6. GetService() calls pre-generated factory (ZERO REFLECTION!)
///
/// TESTING PATH (without Source Generator):
/// =========================================
/// 1. Extension methods like RegisterSingleton<T, TImpl>() create fallback factories
/// 2. Fallback uses Activator.CreateInstance<T>() (AOT-safe generic activator)
/// 3. Allows tests to work without source generator
/// 4. Performance is lower but still reasonable for testing
///
/// PERFORMANCE OPTIMIZATION:
/// =========================
/// Call Build() after all registrations to convert to FrozenDictionary for fastest lookups.
/// Without Build(), ConcurrentDictionary is used (slightly slower but thread-safe for registration).
///
/// RESULT:
/// - ✅ Production: Zero runtime reflection via pre-generated factories
/// - ✅ Testing: Works without source generator
/// - ✅ AOT-compatible: All generated code uses only compile-time-known types
/// - ✅ IL trimmer friendly: No dynamic type discovery
/// - ✅ Compile-time safe: Errors caught during build
/// - ✅ Maximum performance: Direct code execution in production
/// </summary>
public sealed class SvcContainer : ISvcContainer
{
    private readonly ConcurrentDictionary<Type, SvcDescriptor[]> _descriptorCache = new();

    /// <summary>
    /// Frozen (optimized) descriptor cache after Build() is called.
    /// </summary>
    private FrozenDictionary<Type, SvcDescriptor[]>? _frozenCache;

    private int _disposed; // 0 = not disposed, 1 = disposed (for thread-safe Interlocked operations)
    private bool _isBuilt;

    /// <inheritdoc />
    public ISvcContainer Register(SvcDescriptor descriptor)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

        if (_isBuilt)
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
    /// Call this method after all services have been registered.
    /// After calling Build(), no more services can be registered.
    /// </summary>
    /// <returns>The container instance for method chaining.</returns>
    public SvcContainer Build()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

        if (_isBuilt)
            return this;

        _frozenCache = _descriptorCache.ToFrozenDictionary();
        _isBuilt = true;
        return this;
    }

    /// <inheritdoc />
    public ISvcScope CreateScope()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

        // Use frozen cache if available for better performance
        return _frozenCache != null ? new SvcScope(_frozenCache) : new SvcScope(_descriptorCache);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Thread-safe check-and-set using Interlocked
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        foreach (var keyValuePair in _descriptorCache)
        {
            foreach (var svc in keyValuePair.Value)
            {
                if (svc.SingleInstance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
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

        foreach (
            var svc in _descriptorCache
                .SelectMany(p => p.Value)
                .Select(p => p.SingleInstance)
                .Where(p => p is not null)
        )
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
        _descriptorCache.Clear();
    }
}
