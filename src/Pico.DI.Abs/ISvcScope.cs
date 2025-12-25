namespace Pico.DI.Abs;

/// <summary>
/// Represents a service resolution scope for resolving registered services.
/// Supports creating nested scopes and resolving both single and multiple service implementations.
/// </summary>
public interface ISvcScope : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Creates a new nested scope.
    /// </summary>
    /// <returns>A new <see cref="ISvcScope"/> instance.</returns>
    ISvcScope CreateScope();

    /// <summary>
    /// Resolves a service of the specified type.
    /// </summary>
    /// <param name="serviceType">The type of service to resolve.</param>
    /// <returns>The resolved service instance.</returns>
    /// <exception cref="PicoDiException">Thrown when the service is not registered or circular dependency is detected.</exception>
    object GetService(Type serviceType);

    /// <summary>
    /// Resolves all registered services of the specified type.
    /// </summary>
    /// <param name="serviceType">The type of services to resolve.</param>
    /// <returns>An enumerable of all registered service instances.</returns>
    IEnumerable<object> GetServices(Type serviceType);
}

/// <summary>
/// Extension methods for <see cref="ISvcScope"/> providing generic service resolution.
/// </summary>
public static class SvcProviderExtensions
{
    extension(ISvcScope provider)
    {
        /// <summary>
        /// Resolves a service of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of service to resolve.</typeparam>
        /// <returns>The resolved service instance.</returns>
        public T GetService<T>() => (T)provider.GetService(typeof(T));

        /// <summary>
        /// Resolves all registered services of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of services to resolve.</typeparam>
        /// <returns>An enumerable of all registered service instances.</returns>
        public IEnumerable<T> GetServices<T>() => provider.GetServices(typeof(T)).Cast<T>();
    }
}
