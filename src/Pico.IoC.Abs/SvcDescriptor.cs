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

    public object? Instance { get; }

    public Func<ISvcProvider, object>? Factory { get; }

    public SvcLifetime Lifetime { get; } = lifetime;

    public SvcDescriptor(Type serviceType, object instance)
        : this(serviceType, serviceType) =>
        Instance = instance ?? throw new ArgumentNullException(nameof(instance));

    public SvcDescriptor(
        Type serviceType,
        Func<ISvcProvider, object> factory,
        SvcLifetime lifetime = SvcLifetime.Singleton
    )
        : this(serviceType, serviceType, lifetime) =>
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
}
