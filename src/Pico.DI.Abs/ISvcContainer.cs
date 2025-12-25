namespace Pico.DI.Abs;

public interface ISvcContainer : IDisposable, IAsyncDisposable
{
    ISvcContainer Register(SvcDescriptor descriptor);
    ISvcScope CreateScope();
}


public static class SvcContainerExtensions
{
    // Add by type - these are placeholder methods scanned by Source Generator
    // They don't actually register; the generated code with factories does the real registration
    extension(ISvcContainer container)
    {
        public ISvcContainer Register(Type serviceType, Type implementType, SvcLifetime lifetime) =>
            container; // Source Generator will generate factory-based registration

        public ISvcContainer Register(Type serviceType, SvcLifetime lifetime) =>
            container; // Source Generator will generate factory-based registration

        public ISvcContainer Register<TService, TImplementation>(SvcLifetime lifetime)
            where TImplementation : TService =>
            container; // Source Generator will generate factory-based registration

        public ISvcContainer Register<TService>(SvcLifetime lifetime)
            where TService : class =>
            container; // Source Generator will generate factory-based registration

        public ISvcContainer Register<TService>(Type implementType, SvcLifetime lifetime)
            where TService : class =>
            container; // Source Generator will generate factory-based registration
    }

    // Add by factory
    extension(ISvcContainer container)
    {
        public ISvcContainer Register(Type serviceType,
            Func<ISvcScope, object> factory, SvcLifetime lifetime) =>
            container.Register(new SvcDescriptor(serviceType, factory, lifetime));

        public ISvcContainer Register<TService>(Func<ISvcScope, TService> factory, SvcLifetime lifetime)
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(TService), factory, lifetime));

        public ISvcContainer Register<TService, TImplementation>(Func<ISvcScope, TImplementation> factory, SvcLifetime lifetime)
            where TService : class
            where TImplementation : class =>
            container.Register(new SvcDescriptor(typeof(TService), factory, lifetime));
    }

    // Transient
    extension(ISvcContainer container)
    {
        #region Add by type - placeholder methods scanned by Source Generator

        public ISvcContainer RegisterTransient(Type serviceType, Type implementType) =>
            container; // Source Generator will generate factory-based registration

        public ISvcContainer RegisterTransient(Type serviceType) =>
            container; // Source Generator will generate factory-based registration

        public ISvcContainer RegisterTransient<TService, TImplementation>()
            where TImplementation : TService =>
            container; // Source Generator will generate factory-based registration

        public ISvcContainer RegisterTransient<TService>()
            where TService : class =>
            container; // Source Generator will generate factory-based registration

        public ISvcContainer RegisterTransient<TService>(Type implementType)
            where TService : class =>
            container; // Source Generator will generate factory-based registration

        #endregion

        #region Add by factory

        public ISvcContainer RegisterTransient(Type serviceType,
            Func<ISvcScope, object> factory) =>
            container.Register(new SvcDescriptor(serviceType, factory, SvcLifetime.Transient));

        public ISvcContainer RegisterTransient<TService>(Func<ISvcScope, TService> factory)
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(TService), factory, SvcLifetime.Transient));

        public ISvcContainer RegisterTransient<TService, TImplementation>(Func<ISvcScope, TImplementation> factory)
            where TService : class
            where TImplementation : class =>
            container.Register(new SvcDescriptor(typeof(TService), factory, SvcLifetime.Transient));

        #endregion
    }

    // Scoped
    extension(ISvcContainer container)
    {
        #region Add by type - placeholder methods scanned by Source Generator

        public ISvcContainer RegisterScoped(Type serviceType, Type implementType) =>
            container; // Source Generator will generate factory-based registration

        public ISvcContainer RegisterScoped(Type serviceType) =>
            container; // Source Generator will generate factory-based registration

        public ISvcContainer RegisterScoped<TService, TImplementation>()
            where TImplementation : TService =>
            container; // Source Generator will generate factory-based registration

        public ISvcContainer RegisterScoped<TService>()
            where TService : class =>
            container; // Source Generator will generate factory-based registration

        public ISvcContainer RegisterScoped<TService>(Type implementType)
            where TService : class =>
            container; // Source Generator will generate factory-based registration

        #endregion

        #region Add by factory

        public ISvcContainer RegisterScoped(Type serviceType,
            Func<ISvcScope, object> factory) =>
            container.Register(new SvcDescriptor(serviceType, factory, SvcLifetime.Scoped));

        public ISvcContainer RegisterScoped<TService>(Func<ISvcScope, TService> factory)
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(TService), factory, SvcLifetime.Scoped));

        public ISvcContainer RegisterScoped<TService, TImplementation>(Func<ISvcScope, TImplementation> factory)
            where TService : class
            where TImplementation : class =>
            container.Register(new SvcDescriptor(typeof(TService), factory, SvcLifetime.Scoped));

        #endregion
    }

    // Singleton
    extension(ISvcContainer container)
    {
        #region Add by type - placeholder methods scanned by Source Generator

        public ISvcContainer RegisterSingleton(Type serviceType, Type implementType) =>
            container; // Source Generator will generate factory-based registration

        public ISvcContainer RegisterSingleton(Type serviceType) =>
            container; // Source Generator will generate factory-based registration

        public ISvcContainer RegisterSingleton<TService, TImplementation>()
            where TImplementation : TService =>
            container; // Source Generator will generate factory-based registration

        public ISvcContainer RegisterSingleton<TService>()
            where TService : class =>
            container; // Source Generator will generate factory-based registration

        public ISvcContainer RegisterSingleton<TService>(Type implementType)
            where TService : class =>
            container; // Source Generator will generate factory-based registration

        #endregion

        #region Add by factory

        public ISvcContainer RegisterSingleton(Type serviceType,
            Func<ISvcScope, object> factory) =>
            container.Register(new SvcDescriptor(serviceType, factory));

        public ISvcContainer RegisterSingleton<TService>(Func<ISvcScope, TService> factory)
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(TService), factory));

        public ISvcContainer RegisterSingleton<TService, TImplementation>(Func<ISvcScope, TImplementation> factory)
            where TService : class
            where TImplementation : class =>
            container.Register(new SvcDescriptor(typeof(TService), factory));

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

    // Batch registration
    extension(ISvcContainer container)
    {
        /// <summary>
        /// Registers multiple service descriptors at once.
        /// Useful for registering generated descriptors.
        /// </summary>
        public ISvcContainer RegisterRange(IEnumerable<SvcDescriptor> descriptors)
        {
            foreach (var descriptor in descriptors)
            {
                container.Register(descriptor);
            }
            return container;
        }
    }

    // Open Generic registration
    extension(ISvcContainer container)
    {
        /// <summary>
        /// Registers an open generic type.
        /// Example: RegisterOpenGeneric(typeof(IRepository&lt;&gt;), typeof(Repository&lt;&gt;), SvcLifetime.Scoped)
        /// </summary>
        public ISvcContainer RegisterOpenGeneric(
            Type openGenericServiceType,
            Type openGenericImplementationType,
            SvcLifetime lifetime)
        {
            if (!openGenericServiceType.IsGenericTypeDefinition)
                throw new ArgumentException(
                    $"Type '{openGenericServiceType}' must be an open generic type definition.",
                    nameof(openGenericServiceType));

            if (!openGenericImplementationType.IsGenericTypeDefinition)
                throw new ArgumentException(
                    $"Type '{openGenericImplementationType}' must be an open generic type definition.",
                    nameof(openGenericImplementationType));

            return container.Register(new SvcDescriptor(
                openGenericServiceType,
                openGenericImplementationType,
                lifetime));
        }

        /// <summary>
        /// Registers an open generic type as transient.
        /// </summary>
        public ISvcContainer RegisterOpenGenericTransient(
            Type openGenericServiceType,
            Type openGenericImplementationType) =>
            container.RegisterOpenGeneric(openGenericServiceType, openGenericImplementationType, SvcLifetime.Transient);

        /// <summary>
        /// Registers an open generic type as scoped.
        /// </summary>
        public ISvcContainer RegisterOpenGenericScoped(
            Type openGenericServiceType,
            Type openGenericImplementationType) =>
            container.RegisterOpenGeneric(openGenericServiceType, openGenericImplementationType, SvcLifetime.Scoped);

        /// <summary>
        /// Registers an open generic type as singleton.
        /// </summary>
        public ISvcContainer RegisterOpenGenericSingleton(
            Type openGenericServiceType,
            Type openGenericImplementationType) =>
            container.RegisterOpenGeneric(openGenericServiceType, openGenericImplementationType, SvcLifetime.Singleton);
    }
}