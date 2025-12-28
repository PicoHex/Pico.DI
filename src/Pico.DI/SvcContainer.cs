namespace Pico.DI;

/// <summary>
/// The main dependency injection container for registering and managing service descriptors.
/// Implements <see cref="ISvcContainer"/> and supports both synchronous and asynchronous disposal.
/// Also supports decorator generic types for wrapping services at runtime.
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
public partial class SvcContainer : ISvcContainer, ISvcContainerDecorator
{
    private readonly ConcurrentDictionary<Type, SvcDescriptor[]> _descriptorCache = new();

    /// <summary>
    /// Stores registered decorator generic types.
    /// Used by source generator to create decorator factories for closed generic types.
    /// </summary>
    private readonly ConcurrentDictionary<Type, DecoratorMetadata> _decoratorMetadata = new();

    /// <summary>
    /// Frozen (optimized) descriptor cache after Build() is called.
    /// </summary>
    private FrozenDictionary<Type, SvcDescriptor[]>? _frozenCache;

    private bool _disposed;
    private bool _isBuilt;

    /// <inheritdoc />
    public ISvcContainer Register(SvcDescriptor descriptor)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

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
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isBuilt)
            return this;

        _frozenCache = _descriptorCache.ToFrozenDictionary();
        _isBuilt = true;
        return this;
    }

    /// <inheritdoc />
    public ISvcScope CreateScope()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Use frozen cache if available for better performance
        if (_frozenCache != null)
        {
            return new SvcScope(_frozenCache, _decoratorMetadata.ToFrozenDictionary());
        }

        return new SvcScope(_descriptorCache, _decoratorMetadata);
    }

    /// <summary>
    /// Registers a decorator generic type that can wrap any registered service.
    /// The source generator uses this metadata to create closed generic decorators at compile time.
    /// </summary>
    /// <param name="decoratorType">The open generic decorator type (e.g., Logger<>).</param>
    /// <param name="metadata">Metadata describing how to construct the decorator.</param>
    /// <returns>The container for method chaining.</returns>
    public ISvcContainer RegisterDecorator(Type decoratorType, DecoratorMetadata? metadata = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!decoratorType.IsGenericTypeDefinition)
            throw new ArgumentException(
                $"Decorator type must be an open generic (e.g., Logger<>), got '{decoratorType.FullName}'",
                nameof(decoratorType)
            );

        metadata ??= new DecoratorMetadata(decoratorType);
        _decoratorMetadata[decoratorType] = metadata;

        return this;
    }

    /// <summary>
    /// Gets all registered decorator metadata.
    /// Used by source generator to generate decorator factories.
    /// </summary>
    internal IReadOnlyDictionary<Type, DecoratorMetadata> DecoratorMetadata => _decoratorMetadata;

    /// <summary>
    /// Internal implementation of ISvcContainerDecorator interface.
    /// </summary>
    ISvcContainer ISvcContainerDecorator.RegisterDecoratorInternal(
        Type decoratorType,
        DecoratorMetadata metadata
    )
    {
        return RegisterDecorator(decoratorType, metadata);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        if (disposing)
        {
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
        _disposed = true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
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
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
