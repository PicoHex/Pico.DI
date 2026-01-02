namespace Pico.DI.Abs;

/// <summary>
/// Represents a dependency injection container for registering service descriptors and creating scopes.
/// 
/// DESIGN NOTE: Zero-Reflection Compile-Time Architecture
/// ========================================================
/// 
/// This container implements a compile-time factory generation architecture:
/// 
/// 1. Extension methods like RegisterSingleton<T, TImpl>() are PLACEHOLDERS.
///    - They do nothing at runtime (return container unchanged)
///    - They are SCANNED by the Roslyn source generator at compile-time
///    
/// 2. The source generator (Pico.DI.Gen) analyzes these calls and generates
///    a ConfigureGeneratedServices() extension method that:
///    - Analyzes constructor parameters of each implementation type
///    - Generates explicit factory code (no reflection)
///    - Calls this Register(SvcDescriptor) method with pre-built SvcDescriptor instances
///    
/// 3. Each SvcDescriptor contains a pre-compiled factory delegate with all
///    dependencies statically resolved at compile-time
///    
/// 4. At runtime, GetService() simply calls the pre-generated factory
///    (no reflection, no dynamic type discovery)
/// 
/// This design ensures:
/// - ✅ Zero runtime reflection
/// - ✅ AOT-compatible (Native AOT, IL trimming)
/// - ✅ All errors caught at compile-time
/// - ✅ Maximum performance (direct code execution)
/// - ✅ Explicit, debuggable factory code
/// </summary>
public interface ISvcContainer : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Registers a service descriptor in the container.
    /// 
    /// NOTE: This method is called by the source-generated ConfigureGeneratedServices() method
    /// with pre-built SvcDescriptor instances containing compile-time-generated factory delegates.
    /// The SvcDescriptor should already contain a fully-constructed factory; this method
    /// simply adds it to the descriptor cache without any factory generation or reflection.
    /// </summary>
    /// <param name="descriptor">The service descriptor to register (with pre-built factory).</param>
    /// <returns>The container instance for method chaining.</returns>
    ISvcContainer Register(SvcDescriptor descriptor);

    /// <summary>
    /// Creates a new service resolution scope.
    /// </summary>
    /// <returns>A new <see cref="ISvcScope"/> instance.</returns>
    ISvcScope CreateScope();
}

public static class SvcContainerExtensions
{
    // Add by type - these methods handle both regular types and open generics
    // For non-open generics, the Source Generator generates factory-based registration
    // For open generics, they are registered directly with the descriptor
    extension(ISvcContainer container)
    {
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
        /// Placeholder method scanned by source generator. Does nothing at runtime.
        /// Actual registration is generated in ConfigureGeneratedServices().
        /// </summary>
        public ISvcContainer Register<TService, TImplementation>(SvcLifetime lifetime)
            where TImplementation : TService => container;

        /// <summary>
        /// Placeholder method scanned by source generator. Does nothing at runtime.
        /// Actual registration is generated in ConfigureGeneratedServices().
        /// </summary>
        public ISvcContainer Register<TService>(SvcLifetime lifetime)
            where TService : class => container;

        /// <summary>
        /// Placeholder method scanned by source generator. Does nothing at runtime.
        /// Actual registration is generated in ConfigureGeneratedServices().
        /// </summary>
        public ISvcContainer Register<TService>(Type implementType, SvcLifetime lifetime)
            where TService : class => container;
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
        #region Add by type - handles both regular and open generic types

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
        /// Placeholder method scanned by source generator. Does nothing at runtime.
        /// Actual registration is generated in ConfigureGeneratedServices().
        /// </summary>
        public ISvcContainer RegisterTransient<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TImplementation>()
            where TImplementation : TService => container;

        /// <summary>
        /// Placeholder method scanned by source generator. Does nothing at runtime.
        /// Actual registration is generated in ConfigureGeneratedServices().
        /// </summary>
        public ISvcContainer RegisterTransient<TService>()
            where TService : class => container;

        /// <summary>
        /// Placeholder method scanned by source generator. Does nothing at runtime.
        /// Actual registration is generated in ConfigureGeneratedServices().
        /// </summary>
        public ISvcContainer RegisterTransient<TService>(Type implementType)
            where TService : class => container;

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
        #region Add by type - handles both regular and open generic types

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
        /// Placeholder method scanned by source generator. Does nothing at runtime.
        /// Actual registration is generated in ConfigureGeneratedServices().
        /// </summary>
        public ISvcContainer RegisterScoped<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TImplementation>()
            where TImplementation : TService => container;

        /// <summary>
        /// Placeholder method scanned by source generator. Does nothing at runtime.
        /// Actual registration is generated in ConfigureGeneratedServices().
        /// </summary>
        public ISvcContainer RegisterScoped<TService>()
            where TService : class => container;

        /// <summary>
        /// Placeholder method scanned by source generator. Does nothing at runtime.
        /// Actual registration is generated in ConfigureGeneratedServices().
        /// </summary>
        public ISvcContainer RegisterScoped<TService>(Type implementType)
            where TService : class => container;

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
        #region Add by type - handles both regular and open generic types

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
        /// Placeholder method scanned by source generator. Does nothing at runtime.
        /// Actual registration is generated in ConfigureGeneratedServices().
        /// </summary>
        public ISvcContainer RegisterSingleton<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TImplementation>()
            where TImplementation : TService => container;

        /// <summary>
        /// Placeholder method scanned by source generator. Does nothing at runtime.
        /// Actual registration is generated in ConfigureGeneratedServices().
        /// </summary>
        public ISvcContainer RegisterSingleton<TService>()
            where TService : class => container;

        /// <summary>
        /// Placeholder method scanned by source generator. Does nothing at runtime.
        /// Actual registration is generated in ConfigureGeneratedServices().
        /// </summary>
        public ISvcContainer RegisterSingleton<TService>(Type implementType)
            where TService : class => container;

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
}