namespace Pico.IoC;

public class SvcContainer : ISvcContainer
{
    private readonly ConcurrentDictionary<Type, List<SvcDescriptor>> _descriptorCache = new();

    public ISvcContainer Register(SvcDescriptor descriptor) => this;

    public ISvcScope CreateScope() => new SvcScope(_descriptorCache);

    public void Dispose()
    {
        foreach (var keyValuePair in _descriptorCache)
        {
            foreach (var svc in keyValuePair.Value)
            {
                if (svc.Instance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var svc in _descriptorCache.SelectMany(p => p.Value).Select(p => p.Instance))
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
    }
}
