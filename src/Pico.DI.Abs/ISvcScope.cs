namespace Pico.DI.Abs;

public interface ISvcScope : IDisposable, IAsyncDisposable
{
    ISvcScope CreateScope();

    [RequiresDynamicCode("IEnumerable<T> and open generic resolution require dynamic code.")]
    [RequiresUnreferencedCode("Open generic resolution requires reflection.")]
    object GetService(Type serviceType);

    IEnumerable<object> GetServices(Type serviceType);
}

public static class SvcProviderExtensions
{
    extension(ISvcScope provider)
    {
        [RequiresDynamicCode("IEnumerable<T> and open generic resolution require dynamic code.")]
        [RequiresUnreferencedCode("Open generic resolution requires reflection.")]
        public T GetService<T>() => (T)provider.GetService(typeof(T));
        public IEnumerable<T> GetServices<T>() => provider.GetServices(typeof(T)).Cast<T>();
    }
}
