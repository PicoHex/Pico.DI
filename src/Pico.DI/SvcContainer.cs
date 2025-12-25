namespace Pico.DI;

/// <summary>
/// The main dependency injection container for registering and managing service descriptors.
/// Implements <see cref="ISvcContainer"/> and supports both synchronous and asynchronous disposal.
/// </summary>
public partial class SvcContainer : ISvcContainer
{
    private readonly ConcurrentDictionary<Type, List<SvcDescriptor>> _descriptorCache = new();
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
        return new SvcScope(_descriptorCache);
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
