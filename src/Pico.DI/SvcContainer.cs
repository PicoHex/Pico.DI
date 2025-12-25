namespace Pico.DI;

/// <summary>
/// The main dependency injection container for registering and managing service descriptors.
/// Implements <see cref="ISvcContainer"/> and supports both synchronous and asynchronous disposal.
/// Also supports decorator generic types for wrapping services at runtime.
/// </summary>
public partial class SvcContainer : ISvcContainer, ISvcContainerDecorator
{
    private readonly ConcurrentDictionary<Type, List<SvcDescriptor>> _descriptorCache = new();

    /// <summary>
    /// Stores registered decorator generic types.
    /// Used by source generator to create decorator factories for closed generic types.
    /// </summary>
    private readonly ConcurrentDictionary<Type, DecoratorMetadata> _decoratorMetadata = new();

    private bool _disposed;

    /// <inheritdoc />
    public ISvcContainer Register(SvcDescriptor descriptor)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _descriptorCache.AddOrUpdate(
            descriptor.ServiceType,
            _ => [descriptor],
            (_, list) =>
            {
                list.Add(descriptor);
                return list;
            }
        );
        return this;
    }

    /// <inheritdoc />
    public ISvcScope CreateScope()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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
