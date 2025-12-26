namespace Pico.DI.Abs;

/// <summary>
/// Describes a service registration including its service type, factory, and lifetime.
/// For AOT compatibility, services should be registered with factory delegates
/// generated at compile time by Pico.DI.Gen source generator.
/// </summary>
/// <param name="serviceType">The service type (interface or base class) being registered.</param>
/// <param name="implementationType">The concrete implementation type (optional, for open generics).</param>
/// <param name="lifetime">The service lifetime (Transient, Scoped, or Singleton).</param>
public class SvcDescriptor(
    Type serviceType,
    Type? implementationType,
    SvcLifetime lifetime = SvcLifetime.Singleton
)
{
    /// <summary>
    /// Gets the service type (interface or base class) being registered.
    /// </summary>
    public Type ServiceType { get; } =
        serviceType ?? throw new ArgumentNullException(nameof(serviceType));

    /// <summary>
    /// Gets the concrete implementation type.
    /// Used primarily for open generic registrations to track the implementation type.
    /// </summary>
    public Type ImplementationType { get; } = implementationType ?? serviceType;

    /// <summary>
    /// Stores the singleton instance for this service.
    /// This field is accessed using Volatile.Read/Write for lock-free thread-safe access.
    /// </summary>
    public object? SingleInstance;

    /// <summary>
    /// Gets the factory function used to create service instances.
    /// </summary>
    public Func<ISvcScope, object>? Factory { get; }

    /// <summary>
    /// Gets the service lifetime.
    /// </summary>
    public SvcLifetime Lifetime { get; } = lifetime;

    /// <summary>
    /// Creates a service descriptor for a pre-existing singleton instance.
    /// </summary>
    /// <param name="serviceType">The service type being registered.</param>
    /// <param name="instance">The pre-existing instance to register.</param>
    public SvcDescriptor(Type serviceType, object instance)
        : this(serviceType, serviceType) =>
        SingleInstance = instance ?? throw new ArgumentNullException(nameof(instance));

    /// <summary>
    /// Creates a service descriptor with a factory function.
    /// </summary>
    /// <param name="serviceType">The service type being registered.</param>
    /// <param name="factory">The factory function to create instances.</param>
    /// <param name="lifetime">The service lifetime.</param>
    public SvcDescriptor(
        Type serviceType,
        Func<ISvcScope, object> factory,
        SvcLifetime lifetime = SvcLifetime.Singleton
    )
        : this(serviceType, serviceType, lifetime) =>
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
}
