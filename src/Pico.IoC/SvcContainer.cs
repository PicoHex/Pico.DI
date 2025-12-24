namespace Pico.IoC;

public class SvcContainer : ISvcContainer
{
    private readonly ConcurrentDictionary<Type, List<SvcDescriptor>> _descriptorCache = new();
    private bool _disposed;

    public ISvcContainer Register(SvcDescriptor descriptor) => this;

    public ISvcScope CreateScope() => new SvcScope(_descriptorCache);

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
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
