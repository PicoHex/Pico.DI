namespace Pico.DI;

/// <summary>
/// Adapter that wraps SvcScope to implement IServiceProvider.
/// Enables seamless integration with ASP.NET Core and other frameworks
/// that depend on IServiceProvider.
/// </summary>
public sealed class SvcProviderAdapter(ISvcScope scope) : ISvcProviderAdapter
{
    private readonly ISvcScope _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    private bool _disposed;

    /// <summary>
    /// IServiceProvider.GetService implementation.
    /// Returns null if service is not registered (matches IServiceProvider contract).
    /// </summary>
    [RequiresDynamicCode("Creating typed arrays requires dynamic code generation.")]
    public object? GetService(Type serviceType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Handle IEnumerable<T> requests - delegate to scope's GetService which handles this
        if (serviceType.IsGenericType &&
            serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            try
            {
                // Use GetService which handles IEnumerable<T> and returns typed array
                return _scope.GetService(serviceType);
            }
            catch (PicoDiException)
            {
                // Return empty typed array if not registered
                var elementType = serviceType.GetGenericArguments()[0];
                return Array.CreateInstance(elementType, 0);
            }
        }

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
    [RequiresDynamicCode("IEnumerable<T> and open generic resolution require dynamic code.")]
    [RequiresUnreferencedCode("Open generic resolution requires reflection.")]
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
