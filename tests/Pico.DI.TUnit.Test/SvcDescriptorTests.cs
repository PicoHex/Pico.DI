namespace Pico.DI.TUnit.Test;

/// <summary>
/// Tests for SvcDescriptor class.
/// </summary>
public class SvcDescriptorTests : TUnitTestBase
{
    #region Constructor Tests

    [Test]
    public async Task Constructor_WithTypeAndLifetime_SetsProperties()
    {
        // Act
        var descriptor = new SvcDescriptor(
            typeof(IGreeter),
            typeof(ConsoleGreeter),
            SvcLifetime.Transient
        );

        // Assert
        await Assert.That(descriptor.ServiceType).IsEqualTo(typeof(IGreeter));
        await Assert.That(descriptor.ImplementationType).IsEqualTo(typeof(ConsoleGreeter));
        await Assert.That(descriptor.Lifetime).IsEqualTo(SvcLifetime.Transient);
        await Assert.That(descriptor.Factory).IsNull();
        await Assert.That(descriptor.SingleInstance).IsNull();
    }

    [Test]
    public async Task Constructor_WithServiceTypeOnly_ImplementationTypeMatchesService()
    {
        // Act
        var descriptor = new SvcDescriptor(
            typeof(ConsoleGreeter),
            (Type?)null,
            SvcLifetime.Singleton
        );

        // Assert
        await Assert.That(descriptor.ServiceType).IsEqualTo(typeof(ConsoleGreeter));
        await Assert.That(descriptor.ImplementationType).IsEqualTo(typeof(ConsoleGreeter));
    }

    [Test]
    public async Task Constructor_WithFactory_SetsFactory()
    {
        // Arrange
        Func<ISvcScope, object> factory = _ => new ConsoleGreeter();

        // Act
        var descriptor = new SvcDescriptor(typeof(IGreeter), factory, SvcLifetime.Transient);

        // Assert
        await Assert.That(descriptor.Factory).IsSameReferenceAs(factory);
        await Assert.That(descriptor.ServiceType).IsEqualTo(typeof(IGreeter));
        await Assert.That(descriptor.Lifetime).IsEqualTo(SvcLifetime.Transient);
    }

    [Test]
    public async Task Constructor_WithInstance_SetsSingleInstance()
    {
        // Arrange
        var instance = new ConsoleGreeter();

        // Act
        var descriptor = new SvcDescriptor(typeof(IGreeter), instance);

        // Assert
        await Assert.That(descriptor.SingleInstance).IsSameReferenceAs(instance);
        await Assert.That(descriptor.ServiceType).IsEqualTo(typeof(IGreeter));
        await Assert.That(descriptor.Lifetime).IsEqualTo(SvcLifetime.Singleton);
    }

    #endregion

    #region Lifetime Tests

    [Test]
    [Arguments(SvcLifetime.Transient)]
    [Arguments(SvcLifetime.Scoped)]
    [Arguments(SvcLifetime.Singleton)]
    public async Task Constructor_AllLifetimes_WorkCorrectly(SvcLifetime lifetime)
    {
        // Act
        var descriptor = new SvcDescriptor(typeof(IGreeter), typeof(ConsoleGreeter), lifetime);

        // Assert
        await Assert.That(descriptor.Lifetime).IsEqualTo(lifetime);
    }

    [Test]
    public async Task DefaultLifetime_IsSingleton()
    {
        // Act
        var descriptor = new SvcDescriptor(typeof(IGreeter), typeof(ConsoleGreeter));

        // Assert
        await Assert.That(descriptor.Lifetime).IsEqualTo(SvcLifetime.Singleton);
    }

    #endregion

    #region Factory Invocation Tests

    [Test]
    public async Task Factory_WhenInvoked_CreatesInstance()
    {
        // Arrange
        var descriptor = new SvcDescriptor(
            typeof(IGreeter),
            _ => new ConsoleGreeter(),
            SvcLifetime.Transient
        );

        using var container = new SvcContainer();
        using var scope = container.CreateScope();

        // Act
        var instance = descriptor.Factory!(scope);

        // Assert
        await Assert.That(instance).IsNotNull();
        await Assert.That(instance).IsTypeOf<ConsoleGreeter>();
    }

    [Test]
    public async Task Factory_ReceivesScope_CanAccessServices()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);

        var descriptor = new SvcDescriptor(
            typeof(ServiceWithDependency),
            scope => new ServiceWithDependency(scope.GetService<IGreeter>()),
            SvcLifetime.Transient
        );

        using var scope = container.CreateScope();

        // Act
        var instance = descriptor.Factory!(scope) as ServiceWithDependency;

        // Assert
        await Assert.That(instance).IsNotNull();
        await Assert.That(instance!.Greeter).IsNotNull();
    }

    #endregion

    #region Open Generic Tests

    [Test]
    public async Task Constructor_OpenGenericTypes_StoresCorrectly()
    {
        // Act
        var descriptor = new SvcDescriptor(
            typeof(IRepository<>),
            typeof(Repository<>),
            SvcLifetime.Transient
        );

        // Assert
        await Assert.That(descriptor.ServiceType.IsGenericTypeDefinition).IsTrue();
        await Assert.That(descriptor.ImplementationType.IsGenericTypeDefinition).IsTrue();
    }

    #endregion
}
