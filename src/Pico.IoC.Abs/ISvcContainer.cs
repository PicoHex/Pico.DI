namespace Pico.IoC.Abs;

public interface ISvcContainer : IDisposable, IAsyncDisposable
{
    ISvcContainer Register(SvcDescriptor descriptor);
    ISvcScope CreateScope(ISvcContainer container);
}


public static class SvcContainerExtensions
{
    // Add by type
    extension(ISvcContainer container)
    {
        public ISvcContainer Register(Type serviceType, Type implementType, SvcLifetime lifetime) =>
            container.Register(new SvcDescriptor(serviceType, implementType, lifetime));

        public ISvcContainer Register(Type serviceType, SvcLifetime lifetime) =>
            container.Register(new SvcDescriptor(serviceType, serviceType, lifetime));

        public ISvcContainer Register<TService, TImplementation>(SvcLifetime lifetime)
            where TImplementation : TService =>
            container.Register(new SvcDescriptor(typeof(TService), typeof(TImplementation), lifetime));

        public ISvcContainer Register<TService>(SvcLifetime lifetime)
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(TService), typeof(TService), lifetime));

        public ISvcContainer Register<TService>(Type implementType, SvcLifetime lifetime)
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(TService), implementType, lifetime));
    }

    // Add by factory
    extension(ISvcContainer container)
    {
        public ISvcContainer Register(Type serviceType,
            Func<ISvcScope, object> factory, SvcLifetime lifetime) =>
            container.Register(new SvcDescriptor(serviceType, factory, lifetime));

        public ISvcContainer Register<TService>(Func<ISvcScope, TService> factory, SvcLifetime lifetime)
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(ISvcScope), factory, lifetime));

        public ISvcContainer Register<TService, TImplementation>(Func<ISvcScope, TImplementation> factory, SvcLifetime lifetime)
            where TService : class
            where TImplementation : class =>
            container.Register(new SvcDescriptor(typeof(ISvcScope), factory, lifetime));
    }

    // Transient
    extension(ISvcContainer container)
    {
        #region Add by type

        public ISvcContainer RegisterTransient(Type serviceType, Type implementType) =>
            container.Register(new SvcDescriptor(serviceType, implementType, SvcLifetime.Transient));

        public ISvcContainer RegisterTransient(Type serviceType) =>
            container.Register(new SvcDescriptor(serviceType, serviceType, SvcLifetime.Transient));

        public ISvcContainer RegisterTransient<TService, TImplementation>()
            where TImplementation : TService =>
            container.Register(new SvcDescriptor(typeof(TService), typeof(TImplementation), SvcLifetime.Transient));

        public ISvcContainer RegisterTransient<TService>()
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(TService), typeof(TService), SvcLifetime.Transient));

        public ISvcContainer RegisterTransient<TService>(Type implementType)
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(TService), implementType, SvcLifetime.Transient));

        #endregion

        #region Add by factory

        public ISvcContainer RegisterTransient(Type serviceType,
            Func<ISvcScope, object> factory) =>
            container.Register(new SvcDescriptor(serviceType, factory, SvcLifetime.Transient));

        public ISvcContainer RegisterTransient<TService>(Func<ISvcScope, TService> factory)
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(ISvcScope), factory, SvcLifetime.Transient));

        public ISvcContainer RegisterTransient<TService, TImplementation>(Func<ISvcScope, TImplementation> factory)
            where TService : class
            where TImplementation : class =>
            container.Register(new SvcDescriptor(typeof(ISvcScope), factory, SvcLifetime.Transient));

        #endregion
    }

    // Scoped
    extension(ISvcContainer container)
    {
        #region Add by type

        public ISvcContainer RegisterScoped(Type serviceType, Type implementType) =>
            container.Register(new SvcDescriptor(serviceType, implementType, SvcLifetime.Scoped));

        public ISvcContainer RegisterScoped(Type serviceType) =>
            container.Register(new SvcDescriptor(serviceType, serviceType, SvcLifetime.Scoped));

        public ISvcContainer RegisterScoped<TService, TImplementation>()
            where TImplementation : TService =>
            container.Register(new SvcDescriptor(typeof(TService), typeof(TImplementation), SvcLifetime.Scoped));

        public ISvcContainer RegisterScoped<TService>()
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(TService), typeof(TService), SvcLifetime.Scoped));

        public ISvcContainer RegisterScoped<TService>(Type implementType)
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(TService), implementType, SvcLifetime.Scoped));

        #endregion

        #region Add by factory

        public ISvcContainer RegisterScoped(Type serviceType,
            Func<ISvcScope, object> factory) =>
            container.Register(new SvcDescriptor(serviceType, factory, SvcLifetime.Scoped));

        public ISvcContainer RegisterScoped<TService>(Func<ISvcScope, TService> factory)
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(ISvcScope), factory, SvcLifetime.Scoped));

        public ISvcContainer RegisterScoped<TService, TImplementation>(Func<ISvcScope, TImplementation> factory)
            where TService : class
            where TImplementation : class =>
            container.Register(new SvcDescriptor(typeof(ISvcScope), factory, SvcLifetime.Scoped));

        #endregion
    }

    // Singleton
    extension(ISvcContainer container)
    {
        #region Add by type

        public ISvcContainer RegisterSingleton(Type serviceType, Type implementType) =>
            container.Register(new SvcDescriptor(serviceType, implementType));

        public ISvcContainer RegisterSingleton(Type serviceType) =>
            container.Register(new SvcDescriptor(serviceType, serviceType));

        public ISvcContainer RegisterSingleton<TService, TImplementation>()
            where TImplementation : TService =>
            container.Register(new SvcDescriptor(typeof(TService), typeof(TImplementation)));

        public ISvcContainer RegisterSingleton<TService>()
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(TService), typeof(TService)));

        public ISvcContainer RegisterSingleton<TService>(Type implementType)
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(TService), implementType));

        #endregion

        #region Add by factory

        public ISvcContainer RegisterSingleton(Type serviceType,
            Func<ISvcScope, object> factory) =>
            container.Register(new SvcDescriptor(serviceType, factory));

        public ISvcContainer RegisterSingleton<TService>(Func<ISvcScope, TService> factory)
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(ISvcScope), factory));

        public ISvcContainer RegisterSingleton<TService, TImplementation>(Func<ISvcScope, TImplementation> factory)
            where TService : class
            where TImplementation : class =>
            container.Register(new SvcDescriptor(typeof(ISvcScope), factory));

        #endregion

        #region Add by instance

        public ISvcContainer RegisterSingle(
            Type serviceType,
            object instance
        ) => container.Register(new SvcDescriptor(serviceType, instance));

        public ISvcContainer RegisterSingle<TService
        >(object instance) =>
            container.Register(new SvcDescriptor(typeof(TService), instance));

        #endregion
    }
}