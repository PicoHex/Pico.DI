namespace Pico.DI.Abs;

/// <summary>
/// Adapter interface that bridges Pico.DI with System.IServiceProvider.
/// This enables integration with ASP.NET Core and other frameworks.
/// </summary>
public interface ISvcProviderAdapter : IServiceProvider, ISvcScope
{
}

/// <summary>
/// Extension methods for IServiceProvider compatibility.
/// </summary>
public static class ServiceProviderAdapterExtensions
{
    extension(ISvcScope scope)
    {
        /// <summary>
        /// Gets a service of the specified type, returning null if not registered.
        /// This matches IServiceProvider.GetService behavior.
        /// </summary>
        public object? GetServiceOrDefault(Type serviceType)
        {
            try
            {
                return scope.GetService(serviceType);
            }
            catch (PicoDiException)
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a service of the specified type, returning null if not registered.
        /// </summary>
        public T? GetServiceOrDefault<T>() where T : class
        {
            try
            {
                return (T)scope.GetService(typeof(T));
            }
            catch (PicoDiException)
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a required service of the specified type.
        /// Throws InvalidOperationException if not registered (matches Microsoft.Extensions.DependencyInjection behavior).
        /// </summary>
        public object GetRequiredService(Type serviceType)
        {
            try
            {
                return scope.GetService(serviceType);
            }
            catch (PicoDiException ex)
            {
                throw new InvalidOperationException(
                    $"No service for type '{serviceType}' has been registered.",
                    ex
                );
            }
        }

        /// <summary>
        /// Gets a required service of the specified type.
        /// Throws InvalidOperationException if not registered.
        /// </summary>
        public T GetRequiredService<T>() where T : notnull
        {
            try
            {
                return (T)scope.GetService(typeof(T));
            }
            catch (PicoDiException ex)
            {
                throw new InvalidOperationException(
                    $"No service for type '{typeof(T)}' has been registered.",
                    ex
                );
            }
        }
    }
}
