namespace Pico.IoC.Abs;

public class SvcDescriptor(
    Type serviceType,
    Type? implementationType,
    SvcLifetime lifetime = SvcLifetime.Singleton
)
{
    public Type ServiceType { get; } =
        serviceType ?? throw new ArgumentNullException(nameof(serviceType));

    public Type ImplementationType { get; } = implementationType ?? serviceType;

    public object? SingleInstance { get; set; }

    public Func<ISvcScope, object>? Factory { get; }

    public SvcLifetime Lifetime { get; } = lifetime;

    public SvcDescriptor(Type serviceType, object instance)
        : this(serviceType, serviceType) =>
        SingleInstance = instance ?? throw new ArgumentNullException(nameof(instance));

    public SvcDescriptor(
        Type serviceType,
        Func<ISvcScope, object> factory,
        SvcLifetime lifetime = SvcLifetime.Singleton
    )
        : this(serviceType, serviceType, lifetime) =>
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
}
