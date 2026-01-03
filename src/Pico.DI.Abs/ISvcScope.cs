namespace Pico.DI.Abs;

/// <summary>
/// Represents a service scope that manages the lifetime of scoped services and provides service resolution.
/// </summary>
public interface ISvcScope : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Creates a new child service scope.
    /// </summary>
    /// <returns>A new service scope instance.</returns>
    ISvcScope CreateScope();

    /// <summary>
    /// Resolves a service of the specified type.
    /// </summary>
    /// <param name="serviceType">The type of service to resolve.</param>
    /// <returns>The resolved service instance.</returns>
    object GetService(Type serviceType);

    /// <summary>
    /// Resolves all services of the specified type.
    /// </summary>
    /// <param name="serviceType">The type of services to resolve.</param>
    /// <returns>An enumerable of all registered service instances of the specified type.</returns>
    IEnumerable<object> GetServices(Type serviceType);
}

/// <summary>
/// Provides extension methods for <see cref="ISvcScope"/> to simplify service resolution.
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
        /// Resolves all services of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of services to resolve.</typeparam>
        /// <returns>An enumerable of all registered service instances of the specified type.</returns>
        public IEnumerable<T> GetServices<T>() => provider.GetServices(typeof(T)).Cast<T>();
    }
}
