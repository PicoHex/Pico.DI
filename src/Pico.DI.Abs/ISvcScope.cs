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
        /// Uses static generic class caching to avoid typeof(T) overhead on repeated calls.
        /// </summary>
        /// <typeparam name="T">The type of service to resolve.</typeparam>
        /// <returns>The resolved service instance.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetService<T>() => (T)provider.GetService(TypeCache<T>.Value);

        /// <summary>
        /// Resolves all services of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of services to resolve.</typeparam>
        /// <returns>An enumerable of all registered service instances of the specified type.</returns>
        public IEnumerable<T> GetServices<T>() => provider.GetServices(TypeCache<T>.Value).Cast<T>();
    }

    /// <summary>
    /// Static generic class for caching Type objects.
    /// Each unique T gets its own static field, avoiding typeof(T) reflection overhead.
    /// </summary>
    private static class TypeCache<T>
    {
        public static readonly Type Value = typeof(T);
    }
}
