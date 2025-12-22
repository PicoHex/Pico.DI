namespace Pico.IoC.Abs;

public interface ISvcScope : IDisposable, IAsyncDisposable
{
    ISvcScope CreateScope();
    object GetService(Type serviceType);
    IEnumerable<object> GetServices(Type serviceType);
}

public static class SvcProviderExtensions
{
    extension(ISvcScope provider)
    {
        public T GetService<T>() => (T)provider.GetService(typeof(T));
        public IEnumerable<T> GetServices<T>() => provider.GetServices(typeof(T)).Cast<T>();
    }
}
