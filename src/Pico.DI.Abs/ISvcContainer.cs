namespace Pico.DI.Abs;

/// <summary>
/// Represents a dependency injection container that manages service registrations and scope creation.
/// </summary>
public interface ISvcContainer : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Registers a service descriptor with the container.
    /// </summary>
    /// <param name="descriptor">The service descriptor containing service type, factory, and lifetime information.</param>
    /// <returns>The container instance for method chaining.</returns>
    ISvcContainer Register(SvcDescriptor descriptor);

    /// <summary>
    /// Creates a new service scope for resolving scoped services.
    /// </summary>
    /// <returns>A new service scope instance.</returns>
    ISvcScope CreateScope();
}

/// <summary>
/// Provides extension methods for <see cref="ISvcContainer"/> to simplify service registration.
/// </summary>
public static class SvcContainerExtensions
{
    extension(ISvcContainer container)
    {
        /// <summary>
        /// Registers a service with the specified implementation type and lifetime.
        /// </summary>
        /// <param name="serviceType">The service type to register.</param>
        /// <param name="implementType">The implementation type.</param>
        /// <param name="lifetime">The service lifetime.</param>
        /// <returns>The container instance for method chaining.</returns>
        /// <exception cref="SourceGeneratorRequiredException">Thrown when source generator registration is required but not available.</exception>
        public ISvcContainer Register(Type serviceType, Type implementType, SvcLifetime lifetime)
        {
            // Automatically detect and handle open generic types
            if (serviceType.IsGenericTypeDefinition && implementType.IsGenericTypeDefinition)
            {
                return container.Register(new SvcDescriptor(
                    serviceType,
                    implementType,
                    lifetime));
            }

            throw new SourceGeneratorRequiredException(
                "Compile-time generated registrations are required. Ensure Pico.DI.Gen runs and call ConfigureGeneratedServices()."
            );
        }

        /// <summary>
        /// Registers a service type as its own implementation with the specified lifetime.
        /// </summary>
        /// <param name="serviceType">The service type to register (also used as implementation).</param>
        /// <param name="lifetime">The service lifetime.</param>
        /// <returns>The container instance for method chaining.</returns>
        /// <exception cref="SourceGeneratorRequiredException">Thrown when source generator registration is required but not available.</exception>
        public ISvcContainer Register(Type serviceType, SvcLifetime lifetime)
        {
            // Automatically detect and handle open generic types
            if (serviceType.IsGenericTypeDefinition)
            {
                return container.Register(new SvcDescriptor(
                    serviceType,
                    serviceType,
                    lifetime));
            }

            throw new SourceGeneratorRequiredException(
                "Compile-time generated registrations are required. Ensure Pico.DI.Gen runs and call ConfigureGeneratedServices()."
            );
        }

        /// <summary>
        /// Registers a service with the specified implementation type and lifetime.
        /// This is a placeholder for source generator to provide the actual implementation.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <typeparam name="TImplementation">The implementation type.</typeparam>
        /// <param name="lifetime">The service lifetime.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer Register<TService, TImplementation>(SvcLifetime lifetime)
            where TImplementation : TService => container;

        /// <summary>
        /// Registers a service type as its own implementation with the specified lifetime.
        /// This is a placeholder for source generator to provide the actual implementation.
        /// </summary>
        /// <typeparam name="TService">The service type (also used as implementation).</typeparam>
        /// <param name="lifetime">The service lifetime.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer Register<TService>(SvcLifetime lifetime)
            where TService : class => container;

        /// <summary>
        /// Registers a service with the specified implementation type and lifetime.
        /// This is a placeholder for source generator to provide the actual implementation.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <param name="implementType">The implementation type.</param>
        /// <param name="lifetime">The service lifetime.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer Register<TService>(Type implementType, SvcLifetime lifetime)
            where TService : class => container;
    }

    // Add by factory
    extension(ISvcContainer container)
    {
        /// <summary>
        /// Registers a service with a factory function and specified lifetime.
        /// </summary>
        /// <param name="serviceType">The service type to register.</param>
        /// <param name="factory">The factory function to create service instances.</param>
        /// <param name="lifetime">The service lifetime.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer Register(Type serviceType,
            Func<ISvcScope, object> factory, SvcLifetime lifetime) =>
            container.Register(new SvcDescriptor(serviceType, factory, lifetime));

        /// <summary>
        /// Registers a service with a factory function and specified lifetime.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <param name="factory">The factory function to create service instances.</param>
        /// <param name="lifetime">The service lifetime.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer Register<TService>(Func<ISvcScope, TService> factory, SvcLifetime lifetime)
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(TService), factory, lifetime));

        /// <summary>
        /// Registers a service with a factory function and specified lifetime.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <typeparam name="TImplementation">The implementation type returned by the factory.</typeparam>
        /// <param name="factory">The factory function to create service instances.</param>
        /// <param name="lifetime">The service lifetime.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer Register<TService, TImplementation>(Func<ISvcScope, TImplementation> factory, SvcLifetime lifetime)
            where TService : class
            where TImplementation : class =>
            container.Register(new SvcDescriptor(typeof(TService), factory, lifetime));
    }

    // Transient
    extension(ISvcContainer container)
    {
        #region Add by type - handles both regular and open generic types

        /// <summary>
        /// Registers a transient service with the specified implementation type.
        /// A new instance is created each time the service is requested.
        /// </summary>
        /// <param name="serviceType">The service type to register.</param>
        /// <param name="implementType">The implementation type.</param>
        /// <returns>The container instance for method chaining.</returns>
        /// <exception cref="SourceGeneratorRequiredException">Thrown when source generator registration is required but not available.</exception>
        public ISvcContainer RegisterTransient(Type serviceType, Type implementType)
        {
            // Automatically detect and handle open generic types
            if (serviceType.IsGenericTypeDefinition && implementType.IsGenericTypeDefinition)
            {
                return container.Register(new SvcDescriptor(
                    serviceType,
                    implementType,
                    SvcLifetime.Transient));
            }

            throw new SourceGeneratorRequiredException(
                "Compile-time generated registrations are required. Ensure Pico.DI.Gen runs and call ConfigureGeneratedServices()."
            );
        }

        /// <summary>
        /// Registers a transient service type as its own implementation.
        /// A new instance is created each time the service is requested.
        /// </summary>
        /// <param name="serviceType">The service type to register (also used as implementation).</param>
        /// <returns>The container instance for method chaining.</returns>
        /// <exception cref="SourceGeneratorRequiredException">Thrown when source generator registration is required but not available.</exception>
        public ISvcContainer RegisterTransient(Type serviceType)
        {
            // Automatically detect and handle open generic types
            if (serviceType.IsGenericTypeDefinition)
            {
                return container.Register(new SvcDescriptor(
                    serviceType,
                    serviceType,
                    SvcLifetime.Transient));
            }

            throw new SourceGeneratorRequiredException(
                "Compile-time generated registrations are required. Ensure Pico.DI.Gen runs and call ConfigureGeneratedServices()."
            );
        }

        /// <summary>
        /// Registers a transient service with the specified implementation type.
        /// A new instance is created each time the service is requested.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <typeparam name="TImplementation">The implementation type.</typeparam>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterTransient<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TImplementation>()
            where TImplementation : TService => container;

        /// <summary>
        /// Registers a transient service type as its own implementation.
        /// A new instance is created each time the service is requested.
        /// </summary>
        /// <typeparam name="TService">The service type (also used as implementation).</typeparam>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterTransient<TService>()
            where TService : class => container;

        /// <summary>
        /// Registers a transient service with the specified implementation type.
        /// A new instance is created each time the service is requested.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <param name="implementType">The implementation type.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterTransient<TService>(Type implementType)
            where TService : class => container;

        #endregion

        #region Add by factory

        /// <summary>
        /// Registers a transient service with a factory function.
        /// A new instance is created each time the service is requested.
        /// </summary>
        /// <param name="serviceType">The service type to register.</param>
        /// <param name="factory">The factory function to create service instances.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterTransient(Type serviceType,
            Func<ISvcScope, object> factory) =>
            container.Register(new SvcDescriptor(serviceType, factory, SvcLifetime.Transient));

        /// <summary>
        /// Registers a transient service with a factory function.
        /// A new instance is created each time the service is requested.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <param name="factory">The factory function to create service instances.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterTransient<TService>(Func<ISvcScope, TService> factory)
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(TService), factory, SvcLifetime.Transient));

        /// <summary>
        /// Registers a transient service with a factory function.
        /// A new instance is created each time the service is requested.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <typeparam name="TImplementation">The implementation type returned by the factory.</typeparam>
        /// <param name="factory">The factory function to create service instances.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterTransient<TService, TImplementation>(Func<ISvcScope, TImplementation> factory)
            where TService : class
            where TImplementation : class =>
            container.Register(new SvcDescriptor(typeof(TService), factory, SvcLifetime.Transient));

        #endregion
    }

    // Scoped
    extension(ISvcContainer container)
    {
        #region Add by type - handles both regular and open generic types

        /// <summary>
        /// Registers a scoped service with the specified implementation type.
        /// A single instance is created per scope.
        /// </summary>
        /// <param name="serviceType">The service type to register.</param>
        /// <param name="implementType">The implementation type.</param>
        /// <returns>The container instance for method chaining.</returns>
        /// <exception cref="SourceGeneratorRequiredException">Thrown when source generator registration is required but not available.</exception>
        public ISvcContainer RegisterScoped(Type serviceType, Type implementType)
        {
            // Automatically detect and handle open generic types
            if (serviceType.IsGenericTypeDefinition && implementType.IsGenericTypeDefinition)
            {
                return container.Register(new SvcDescriptor(
                    serviceType,
                    implementType,
                    SvcLifetime.Scoped));
            }

            throw new SourceGeneratorRequiredException(
                "Compile-time generated registrations are required. Ensure Pico.DI.Gen runs and call ConfigureGeneratedServices()."
            );
        }

        /// <summary>
        /// Registers a scoped service type as its own implementation.
        /// A single instance is created per scope.
        /// </summary>
        /// <param name="serviceType">The service type to register (also used as implementation).</param>
        /// <returns>The container instance for method chaining.</returns>
        /// <exception cref="SourceGeneratorRequiredException">Thrown when source generator registration is required but not available.</exception>
        public ISvcContainer RegisterScoped(Type serviceType)
        {
            // Automatically detect and handle open generic types
            if (serviceType.IsGenericTypeDefinition)
            {
                return container.Register(new SvcDescriptor(
                    serviceType,
                    serviceType,
                    SvcLifetime.Scoped));
            }

            throw new SourceGeneratorRequiredException(
                "Compile-time generated registrations are required. Ensure Pico.DI.Gen runs and call ConfigureGeneratedServices()."
            );
        }

        /// <summary>
        /// Registers a scoped service with the specified implementation type.
        /// A single instance is created per scope.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <typeparam name="TImplementation">The implementation type.</typeparam>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterScoped<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TImplementation>()
            where TImplementation : TService => container;

        /// <summary>
        /// Registers a scoped service type as its own implementation.
        /// A single instance is created per scope.
        /// </summary>
        /// <typeparam name="TService">The service type (also used as implementation).</typeparam>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterScoped<TService>()
            where TService : class => container;

        /// <summary>
        /// Registers a scoped service with the specified implementation type.
        /// A single instance is created per scope.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <param name="implementType">The implementation type.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterScoped<TService>(Type implementType)
            where TService : class => container;

        #endregion

        #region Add by factory

        /// <summary>
        /// Registers a scoped service with a factory function.
        /// A single instance is created per scope.
        /// </summary>
        /// <param name="serviceType">The service type to register.</param>
        /// <param name="factory">The factory function to create service instances.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterScoped(Type serviceType,
            Func<ISvcScope, object> factory) =>
            container.Register(new SvcDescriptor(serviceType, factory, SvcLifetime.Scoped));

        /// <summary>
        /// Registers a scoped service with a factory function.
        /// A single instance is created per scope.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <param name="factory">The factory function to create service instances.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterScoped<TService>(Func<ISvcScope, TService> factory)
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(TService), factory, SvcLifetime.Scoped));

        /// <summary>
        /// Registers a scoped service with a factory function.
        /// A single instance is created per scope.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <typeparam name="TImplementation">The implementation type returned by the factory.</typeparam>
        /// <param name="factory">The factory function to create service instances.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterScoped<TService, TImplementation>(Func<ISvcScope, TImplementation> factory)
            where TService : class
            where TImplementation : class =>
            container.Register(new SvcDescriptor(typeof(TService), factory, SvcLifetime.Scoped));

        #endregion
    }

    // Singleton
    extension(ISvcContainer container)
    {
        #region Add by type - handles both regular and open generic types

        /// <summary>
        /// Registers a singleton service with the specified implementation type.
        /// A single instance is shared across the entire application.
        /// </summary>
        /// <param name="serviceType">The service type to register.</param>
        /// <param name="implementType">The implementation type.</param>
        /// <returns>The container instance for method chaining.</returns>
        /// <exception cref="SourceGeneratorRequiredException">Thrown when source generator registration is required but not available.</exception>
        public ISvcContainer RegisterSingleton(Type serviceType, Type implementType)
        {
            // Automatically detect and handle open generic types
            if (serviceType.IsGenericTypeDefinition && implementType.IsGenericTypeDefinition)
            {
                return container.Register(new SvcDescriptor(
                    serviceType,
                    implementType,
                    SvcLifetime.Singleton));
            }

            throw new SourceGeneratorRequiredException(
                "Compile-time generated registrations are required. Ensure Pico.DI.Gen runs and call ConfigureGeneratedServices()."
            );
        }

        /// <summary>
        /// Registers a singleton service type as its own implementation.
        /// A single instance is shared across the entire application.
        /// </summary>
        /// <param name="serviceType">The service type to register (also used as implementation).</param>
        /// <returns>The container instance for method chaining.</returns>
        /// <exception cref="SourceGeneratorRequiredException">Thrown when source generator registration is required but not available.</exception>
        public ISvcContainer RegisterSingleton(Type serviceType)
        {
            // Automatically detect and handle open generic types
            if (serviceType.IsGenericTypeDefinition)
            {
                return container.Register(new SvcDescriptor(
                    serviceType,
                    serviceType,
                    SvcLifetime.Singleton));
            }

            throw new SourceGeneratorRequiredException(
                "Compile-time generated registrations are required. Ensure Pico.DI.Gen runs and call ConfigureGeneratedServices()."
            );
        }

        /// <summary>
        /// Registers a singleton service with the specified implementation type.
        /// A single instance is shared across the entire application.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <typeparam name="TImplementation">The implementation type.</typeparam>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterSingleton<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TImplementation>()
            where TImplementation : TService => container;

        /// <summary>
        /// Registers a singleton service type as its own implementation.
        /// A single instance is shared across the entire application.
        /// </summary>
        /// <typeparam name="TService">The service type (also used as implementation).</typeparam>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterSingleton<TService>()
            where TService : class => container;

        /// <summary>
        /// Registers a singleton service with the specified implementation type.
        /// A single instance is shared across the entire application.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <param name="implementType">The implementation type.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterSingleton<TService>(Type implementType)
            where TService : class => container;

        #endregion

        #region Add by factory

        /// <summary>
        /// Registers a singleton service with a factory function.
        /// A single instance is shared across the entire application.
        /// </summary>
        /// <param name="serviceType">The service type to register.</param>
        /// <param name="factory">The factory function to create the service instance.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterSingleton(Type serviceType,
            Func<ISvcScope, object> factory) =>
            container.Register(new SvcDescriptor(serviceType, factory));

        /// <summary>
        /// Registers a singleton service with a factory function.
        /// A single instance is shared across the entire application.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <param name="factory">The factory function to create the service instance.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterSingleton<TService>(Func<ISvcScope, TService> factory)
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(TService), factory));

        /// <summary>
        /// Registers a singleton service with a factory function.
        /// A single instance is shared across the entire application.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <typeparam name="TImplementation">The implementation type returned by the factory.</typeparam>
        /// <param name="factory">The factory function to create the service instance.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterSingleton<TService, TImplementation>(Func<ISvcScope, TImplementation> factory)
            where TService : class
            where TImplementation : class =>
            container.Register(new SvcDescriptor(typeof(TService), factory));

        #endregion

        #region Add by instance

        /// <summary>
        /// Registers a pre-created instance as a singleton service.
        /// </summary>
        /// <param name="serviceType">The service type to register.</param>
        /// <param name="instance">The pre-created instance.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterSingle(
            Type serviceType,
            object instance
        ) => container.Register(new SvcDescriptor(serviceType, instance));

        /// <summary>
        /// Registers a pre-created instance as a singleton service.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <param name="instance">The pre-created instance.</param>
        /// <returns>The container instance for method chaining.</returns>
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
        /// </summary>
        /// <param name="descriptors">The collection of service descriptors to register.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterRange(IEnumerable<SvcDescriptor> descriptors)
        {
            foreach (var descriptor in descriptors)
            {
                container.Register(descriptor);
            }

            return container;
        }
    }
}
rns>
        public ISvcContainer RegisterScoped<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TImplementation>()
            where TImplementation : TService => container;

        /// <summary>
        /// Registers a scoped service type as its own implementation.
        /// A single instance is created per scope.
        /// </summary>
        /// <typeparam name="TService">The service type (also used as implementation).</typeparam>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterScoped<TService>()
            where TService : class => container;

        /// <summary>
        /// Registers a scoped service with the specified implementation type.
        /// A single instance is created per scope.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <param name="implementType">The implementation type.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterScoped<TService>(Type implementType)
            where TService : class => container;

        #endregion

        #region Add by factory

        /// <summary>
        /// Registers a scoped service with a factory function.
        /// A single instance is created per scope.
        /// </summary>
        /// <param name="serviceType">The service type to register.</param>
        /// <param name="factory">The factory function to create service instances.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterScoped(Type serviceType,
            Func<ISvcScope, object> factory) =>
            container.Register(new SvcDescriptor(serviceType, factory, SvcLifetime.Scoped));

        /// <summary>
        /// Registers a scoped service with a factory function.
        /// A single instance is created per scope.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <param name="factory">The factory function to create service instances.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterScoped<TService>(Func<ISvcScope, TService> factory)
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(TService), factory, SvcLifetime.Scoped));

        /// <summary>
        /// Registers a scoped service with a factory function.
        /// A single instance is created per scope.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <typeparam name="TImplementation">The implementation type returned by the factory.</typeparam>
        /// <param name="factory">The factory function to create service instances.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterScoped<TService, TImplementation>(Func<ISvcScope, TImplementation> factory)
            where TService : class
            where TImplementation : class =>
            container.Register(new SvcDescriptor(typeof(TService), factory, SvcLifetime.Scoped));

        #endregion
    }

    // Singleton
    extension(ISvcContainer container)
    {
        #region Add by type - handles both regular and open generic types

        /// <summary>
        /// Registers a singleton service with the specified implementation type.
        /// A single instance is shared across the entire application.
        /// </summary>
        /// <param name="serviceType">The service type to register.</param>
        /// <param name="implementType">The implementation type.</param>
        /// <returns>The container instance for method chaining.</returns>
        /// <exception cref="SourceGeneratorRequiredException">Thrown when source generator registration is required but not available.</exception>
        public ISvcContainer RegisterSingleton(Type serviceType, Type implementType)
        {
            // Automatically detect and handle open generic types
            if (serviceType.IsGenericTypeDefinition && implementType.IsGenericTypeDefinition)
            {
                return container.Register(new SvcDescriptor(
                    serviceType,
                    implementType,
                    SvcLifetime.Singleton));
            }

            throw new SourceGeneratorRequiredException(
                "Compile-time generated registrations are required. Ensure Pico.DI.Gen runs and call ConfigureGeneratedServices()."
            );
        }

        /// <summary>
        /// Registers a singleton service type as its own implementation.
        /// A single instance is shared across the entire application.
        /// </summary>
        /// <param name="serviceType">The service type to register (also used as implementation).</param>
        /// <returns>The container instance for method chaining.</returns>
        /// <exception cref="SourceGeneratorRequiredException">Thrown when source generator registration is required but not available.</exception>
        public ISvcContainer RegisterSingleton(Type serviceType)
        {
            // Automatically detect and handle open generic types
            if (serviceType.IsGenericTypeDefinition)
            {
                return container.Register(new SvcDescriptor(
                    serviceType,
                    serviceType,
                    SvcLifetime.Singleton));
            }

            throw new SourceGeneratorRequiredException(
                "Compile-time generated registrations are required. Ensure Pico.DI.Gen runs and call ConfigureGeneratedServices()."
            );
        }

        /// <summary>
        /// Registers a singleton service with the specified implementation type.
        /// A single instance is shared across the entire application.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <typeparam name="TImplementation">The implementation type.</typeparam>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterSingleton<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TImplementation>()
            where TImplementation : TService => container;

        /// <summary>
        /// Registers a singleton service type as its own implementation.
        /// A single instance is shared across the entire application.
        /// </summary>
        /// <typeparam name="TService">The service type (also used as implementation).</typeparam>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterSingleton<TService>()
            where TService : class => container;

        /// <summary>
        /// Registers a singleton service with the specified implementation type.
        /// A single instance is shared across the entire application.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <param name="implementType">The implementation type.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterSingleton<TService>(Type implementType)
            where TService : class => container;

        #endregion

        #region Add by factory

        /// <summary>
        /// Registers a singleton service with a factory function.
        /// A single instance is shared across the entire application.
        /// </summary>
        /// <param name="serviceType">The service type to register.</param>
        /// <param name="factory">The factory function to create the service instance.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterSingleton(Type serviceType,
            Func<ISvcScope, object> factory) =>
            container.Register(new SvcDescriptor(serviceType, factory));

        /// <summary>
        /// Registers a singleton service with a factory function.
        /// A single instance is shared across the entire application.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <param name="factory">The factory function to create the service instance.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterSingleton<TService>(Func<ISvcScope, TService> factory)
            where TService : class =>
            container.Register(new SvcDescriptor(typeof(TService), factory));

        /// <summary>
        /// Registers a singleton service with a factory function.
        /// A single instance is shared across the entire application.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <typeparam name="TImplementation">The implementation type returned by the factory.</typeparam>
        /// <param name="factory">The factory function to create the service instance.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterSingleton<TService, TImplementation>(Func<ISvcScope, TImplementation> factory)
            where TService : class
            where TImplementation : class =>
            container.Register(new SvcDescriptor(typeof(TService), factory));

        #endregion

        #region Add by instance

        /// <summary>
        /// Registers a pre-created instance as a singleton service.
        /// </summary>
        /// <param name="serviceType">The service type to register.</param>
        /// <param name="instance">The pre-created instance.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterSingle(
            Type serviceType,
            object instance
        ) => container.Register(new SvcDescriptor(serviceType, instance));

        /// <summary>
        /// Registers a pre-created instance as a singleton service.
        /// </summary>
        /// <typeparam name="TService">The service type.</typeparam>
        /// <param name="instance">The pre-created instance.</param>
        /// <returns>The container instance for method chaining.</returns>
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
        /// </summary>
        /// <param name="descriptors">The collection of service descriptors to register.</param>
        /// <returns>The container instance for method chaining.</returns>
        public ISvcContainer RegisterRange(IEnumerable<SvcDescriptor> descriptors)
        {
            foreach (var descriptor in descriptors)
            {
                container.Register(descriptor);
            }

            return container;
        }
    }
}
