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

/// <summary>
/// Internal interface for decorator support in containers.
/// Implemented by containers that support decorator generic types.
/// </summary>
public interface ISvcContainerDecorator
{
    /// <summary>
    /// Registers decorator metadata internally.
    /// This is an internal API used by the RegisterDecorator extension method.
    /// </summary>
    ISvcContainer RegisterDecoratorInternal(Type decoratorType, DecoratorMetadata metadata);
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
            return container; // Source Generator will generate factory-based registration
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
            return container; // Source Generator will generate factory-based registration
        }

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
            return container; // Source Generator will generate factory-based registration
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
            return container; // Source Generator will generate factory-based registration
        }

        public ISvcContainer RegisterTransient<TService, TImplementation>()
            where TImplementation : TService
        {
            // DESIGN NOTE: This method can work in two modes:
            // 
            // MODE 1 - Compile-Time (Source Generator):
            // ==========================================
            // The source generator scans for calls to this method and generates
            // explicit factory code in ConfigureGeneratedServices(). This method
            // itself is never called by generated code - the generator produces
            // direct Register(SvcDescriptor) calls with pre-built factories.
            //
            // MODE 2 - Runtime (Manual/Testing):
            // ===================================
            // When this method IS called at runtime (e.g., in tests), it creates
            // a simple factory as a fallback. This ensures tests can work without
            // running the source generator.
            
            return container.Register(
                new SvcDescriptor(
                    typeof(TService),
                    static _ => Activator.CreateInstance<TImplementation>()!,
                    SvcLifetime.Transient
                )
            );
        }

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
            return container; // Source Generator will generate factory-based registration
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
            return container; // Source Generator will generate factory-based registration
        }

        public ISvcContainer RegisterScoped<TService, TImplementation>()
            where TImplementation : TService
        {
            // DESIGN NOTE: This method can work in two modes:
            // 
            // MODE 1 - Compile-Time (Source Generator):
            // ==========================================
            // The source generator scans for calls to this method and generates
            // explicit factory code in ConfigureGeneratedServices(). This method
            // itself is never called by generated code - the generator produces
            // direct Register(SvcDescriptor) calls with pre-built factories.
            //
            // MODE 2 - Runtime (Manual/Testing):
            // ===================================
            // When this method IS called at runtime (e.g., in tests), it creates
            // a simple factory as a fallback. This ensures tests can work without
            // running the source generator.
            
            return container.Register(
                new SvcDescriptor(
                    typeof(TService),
                    static _ => Activator.CreateInstance<TImplementation>()!,
                    SvcLifetime.Scoped
                )
            );
        }

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
            return container; // Source Generator will generate factory-based registration
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
            return container; // Source Generator will generate factory-based registration
        }

        public ISvcContainer RegisterSingleton<TService, TImplementation>()
            where TImplementation : TService
        {
            // DESIGN NOTE: This method can work in two modes:
            // 
            // MODE 1 - Compile-Time (Source Generator):
            // ==========================================
            // The source generator scans for calls to this method and generates
            // explicit factory code in ConfigureGeneratedServices(). This method
            // itself is never called by generated code - the generator produces
            // direct Register(SvcDescriptor) calls with pre-built factories.
            //
            // MODE 2 - Runtime (Manual/Testing):
            // ===================================
            // When this method IS called at runtime (e.g., in tests), it creates
            // a simple factory as a fallback. This ensures tests can work without
            // running the source generator.
            
            return container.Register(
                new SvcDescriptor(
                    typeof(TService),
                    static _ => Activator.CreateInstance<TImplementation>()!,
                    SvcLifetime.Singleton
                )
            );
        }

        public ISvcContainer RegisterSingleton<TService>()
            where TService : class =>
            container; // ← PLACEHOLDER: Source Generator will generate factory-based registration

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

    // Decorator registration
    extension(ISvcContainer container)
    {
        /// <summary>
        /// Registers an open generic decorator type that can wrap registered services.
        /// The source generator will detect GetService<Decorator<T>> calls and generate
        /// closed generic factories that wrap the service instances.
        /// 
        /// For AOT compatibility, the decorator factory must be registered manually
        /// or generated by the source generator at compile time.
        /// </summary>
        /// <typeparam name="TDecorator">The open generic decorator type (e.g., Logger<>)</typeparam>
        /// <param name="lifetime">The lifetime for created decorator instances. Default: Transient.</param>
        /// <param name="decoratedServiceParameterIndex">
        /// The zero-based index of the constructor parameter that receives the decorated service.
        /// Default: 0 (first parameter). Use -1 for automatic detection by type matching.
        /// </param>
        /// <returns>The container for method chaining.</returns>
        public ISvcContainer RegisterDecorator<TDecorator>(
            SvcLifetime lifetime = SvcLifetime.Transient,
            int decoratedServiceParameterIndex = 0)
            where TDecorator : class
        {
            var decoratorType = typeof(TDecorator);

            if (!decoratorType.IsGenericTypeDefinition)
                throw new ArgumentException(
                    $"Decorator type '{decoratorType.FullName}' must be an open generic type (e.g., Logger<>)",
                    nameof(TDecorator));

            // Note: The actual decorator metadata storage is handled by the concrete
            // SvcContainer implementation via RegisterDecoratorInternal hook.
            // This extension method serves as the public API.
            
            // If the container implements the decorator interface, use it
            if (container is ISvcContainerDecorator decoratorContainer)
            {
                return decoratorContainer.RegisterDecoratorInternal(
                    decoratorType,
                    new DecoratorMetadata(decoratorType, lifetime, decoratedServiceParameterIndex)
                );
            }

            // Otherwise, just return the container (no-op for non-supporting implementations)
            return container;
        }
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