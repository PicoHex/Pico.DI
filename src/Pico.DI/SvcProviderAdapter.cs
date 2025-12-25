namespace Pico.DI;

/// <summary>
/// Adapter that wraps SvcScope to implement IServiceProvider.
/// Enables seamless integration with ASP.NET Core and other frameworks
/// that depend on IServiceProvider.
/// For AOT compatibility, IEnumerable&lt;T&gt; should be explicitly registered
/// using the source generator or manual factory registration.
/// </summary>
public sealed class SvcProviderAdapter(ISvcScope scope) : ISvcProviderAdapter
{
    private readonly ISvcScope _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    private bool _disposed;

    /// <summary>
    /// IServiceProvider.GetService implementation.
    /// Returns null if service is not registered (matches IServiceProvider contract).
    /// </summary>
    public object? GetService(Type serviceType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            return _scope.GetService(serviceType);
        }
        catch (PicoDiException)
        {
            return null;
        }
    }

    /// <summary>
    /// ISvcScope.GetService implementation.
    /// Throws if service is not registered.
    /// </summary>
    object ISvcScope.GetService(Type serviceType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _scope.GetService(serviceType);
    }

    public IEnumerable<object> GetServices(Type serviceType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _scope.GetServices(serviceType);
    }

    public ISvcScope CreateScope()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new SvcProviderAdapter(_scope.CreateScope());
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _scope.Dispose();
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        await _scope.DisposeAsync();
        _disposed = true;
    }
}

/// <summary>
/// Extension methods to create IServiceProvider adapters.
/// </summary>
public static class SvcProviderAdapterExtensions
{
    extension(ISvcContainer container)
    {
        /// <summary>
        /// Creates a scope that implements IServiceProvider.
        /// </summary>
        public ISvcProviderAdapter CreateServiceProviderScope() =>
            new SvcProviderAdapter(container.CreateScope());
    }

    extension(ISvcScope scope)
    {
        /// <summary>
        /// Wraps this scope in an IServiceProvider adapter.
        /// </summary>
        public ISvcProviderAdapter AsServiceProvider() =>
            scope is ISvcProviderAdapter adapter ? adapter : new SvcProviderAdapter(scope);
    }
}
