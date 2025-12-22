namespace Pico.IoC;

public class SvcContainer : ISvcContainer
{
    private readonly ConcurrentDictionary<Type, List<SvcDescriptor>> _descriptorCache = new();

    public ISvcContainer Register(SvcDescriptor descriptor) => this;

    public ISvcScope CreateScope(ISvcContainer container) => new SvcScope(_descriptorCache);

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
        Dispose();
        foreach (var keyValuePair in _descriptorCache)
        {
            foreach (var svc in keyValuePair.Value)
            {
                if (svc.Instance is IAsyncDisposable disposable)
                {
                    await disposable.DisposeAsync();
                }
            }
        }
    }
}
