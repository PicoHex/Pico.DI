namespace Pico.DI.TUnit.Test;

/// <summary>
/// Tests for factory-based service registration.
/// </summary>
public class SvcContainerFactoryRegistrationTests : TUnitTestBase
{
    #region Register by Factory - Non-Generic

    [Test]
    public async Task Register_ByFactory_NonGeneric_WithLifetime_ResolvesService()
    {
        // Arrange
        using var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.Register(
            typeof(IGreeter),
            _ =>
            {
                callCount++;
                return new ConsoleGreeter();
            },
            SvcLifetime.Transient
        );

        using var scope = container.CreateScope();
        var service1 = scope.GetService(typeof(IGreeter));
        var service2 = scope.GetService(typeof(IGreeter));

        // Assert
        await Assert.That(service1).IsNotNull();
        await Assert.That(callCount).IsEqualTo(2);
    }

    #endregion

    #region Register by Factory - Generic

    [Test]
    public async Task Register_ByFactory_Generic_WithLifetime_ResolvesService()
    {
        // Arrange
        using var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.Register<IGreeter>(
            _ =>
            {
                callCount++;
                return new ConsoleGreeter();
            },
            SvcLifetime.Transient
        );

        using var scope = container.CreateScope();
        scope.GetService<IGreeter>();
        scope.GetService<IGreeter>();

        // Assert
        await Assert.That(callCount).IsEqualTo(2);
    }

    [Test]
    public async Task Register_ByFactory_GenericServiceAndImpl_WithLifetime()
    {
        // Arrange
        using var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.Register<IGreeter, ConsoleGreeter>(
            _ =>
            {
                callCount++;
                return new ConsoleGreeter();
            },
            SvcLifetime.Transient
        );

        using var scope = container.CreateScope();
        scope.GetService<IGreeter>();
        scope.GetService<IGreeter>();

        // Assert
        await Assert.That(callCount).IsEqualTo(2);
    }

    #endregion

    #region RegisterTransient by Factory

    [Test]
    public async Task RegisterTransient_ByFactory_NonGeneric_CreatesNewInstanceEachTime()
    {
        // Arrange
        using var container = new SvcContainer();
        var callCount = 0;

        container.RegisterTransient(
            typeof(IGreeter),
            _ =>
            {
                callCount++;
                return new ConsoleGreeter();
            }
        );

        using var scope = container.CreateScope();

        // Act
        scope.GetService(typeof(IGreeter));
        scope.GetService(typeof(IGreeter));
        scope.GetService(typeof(IGreeter));

        // Assert
        await Assert.That(callCount).IsEqualTo(3);
    }

    [Test]
    public async Task RegisterTransient_ByFactory_Generic_CreatesNewInstanceEachTime()
    {
        // Arrange
        using var container = new SvcContainer();
        var instances = new List<IGreeter>();

        container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());

        using var scope = container.CreateScope();

        // Act
        var instance1 = scope.GetService<IGreeter>();
        var instance2 = scope.GetService<IGreeter>();

        // Assert
        await Assert.That(instance1).IsNotSameReferenceAs(instance2);
    }

    [Test]
    public async Task RegisterTransient_ByFactory_GenericServiceAndImpl()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<IGreeter, ConsoleGreeter>(_ => new ConsoleGreeter());

        using var scope = container.CreateScope();

        // Act
        var instance1 = scope.GetService<IGreeter>();
        var instance2 = scope.GetService<IGreeter>();

        // Assert
        await Assert.That(instance1).IsNotSameReferenceAs(instance2);
    }

    #endregion

    #region RegisterScoped by Factory

    [Test]
    public async Task RegisterScoped_ByFactory_NonGeneric_SameInstanceWithinScope()
    {
        // Arrange
        using var container = new SvcContainer();
        var callCount = 0;

        container.RegisterScoped(
            typeof(IGreeter),
            _ =>
            {
                callCount++;
                return new ConsoleGreeter();
            }
        );

        // Act
        using var scope = container.CreateScope();
        var instance1 = scope.GetService(typeof(IGreeter));
        var instance2 = scope.GetService(typeof(IGreeter));

        // Assert
        await Assert.That(callCount).IsEqualTo(1);
        await Assert.That(instance1).IsSameReferenceAs(instance2);
    }

    [Test]
    public async Task RegisterScoped_ByFactory_Generic_SameInstanceWithinScope()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterScoped<IGreeter>(_ => new ConsoleGreeter());

        using var scope = container.CreateScope();

        // Act
        var instance1 = scope.GetService<IGreeter>();
        var instance2 = scope.GetService<IGreeter>();

        // Assert
        await Assert.That(instance1).IsSameReferenceAs(instance2);
    }

    [Test]
    public async Task RegisterScoped_ByFactory_DifferentScopes_DifferentInstances()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterScoped<IGreeter>(_ => new ConsoleGreeter());

        // Act
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();
        var instance1 = scope1.GetService<IGreeter>();
        var instance2 = scope2.GetService<IGreeter>();

        // Assert
        await Assert.That(instance1).IsNotSameReferenceAs(instance2);
    }

    [Test]
    public async Task RegisterScoped_ByFactory_GenericServiceAndImpl()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterScoped<IGreeter, ConsoleGreeter>(_ => new ConsoleGreeter());

        using var scope = container.CreateScope();

        // Act
        var instance1 = scope.GetService<IGreeter>();
        var instance2 = scope.GetService<IGreeter>();

        // Assert
        await Assert.That(instance1).IsSameReferenceAs(instance2);
    }

    #endregion

    #region RegisterSingleton by Factory

    [Test]
    public async Task RegisterSingleton_ByFactory_NonGeneric_SameInstanceGlobally()
    {
        // Arrange
        using var container = new SvcContainer();
        var callCount = 0;

        container.RegisterSingleton(
            typeof(IGreeter),
            _ =>
            {
                callCount++;
                return new ConsoleGreeter();
            }
        );

        // Act
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();
        var instance1 = scope1.GetService(typeof(IGreeter));
        var instance2 = scope2.GetService(typeof(IGreeter));

        // Assert
        await Assert.That(callCount).IsEqualTo(1);
        await Assert.That(instance1).IsSameReferenceAs(instance2);
    }

    [Test]
    public async Task RegisterSingleton_ByFactory_Generic_SameInstanceGlobally()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterSingleton<IGreeter>(_ => new ConsoleGreeter());

        // Act
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();
        var instance1 = scope1.GetService<IGreeter>();
        var instance2 = scope2.GetService<IGreeter>();

        // Assert
        await Assert.That(instance1).IsSameReferenceAs(instance2);
    }

    [Test]
    public async Task RegisterSingleton_ByFactory_GenericServiceAndImpl()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterSingleton<IGreeter, ConsoleGreeter>(_ => new ConsoleGreeter());

        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        // Act
        var instance1 = scope1.GetService<IGreeter>();
        var instance2 = scope2.GetService<IGreeter>();

        // Assert
        await Assert.That(instance1).IsSameReferenceAs(instance2);
    }

    #endregion

    #region RegisterSingle by Instance

    [Test]
    public async Task RegisterSingle_ByInstance_NonGeneric_ReturnsSameInstance()
    {
        // Arrange
        using var container = new SvcContainer();
        var instance = new ConsoleGreeter();

        container.RegisterSingle(typeof(IGreeter), instance);

        using var scope = container.CreateScope();

        // Act
        var resolved = scope.GetService(typeof(IGreeter));

        // Assert
        await Assert.That(resolved).IsSameReferenceAs(instance);
    }

    [Test]
    public async Task RegisterSingle_ByInstance_Generic_ReturnsSameInstance()
    {
        // Arrange
        using var container = new SvcContainer();
        var instance = new ConsoleGreeter();

        container.RegisterSingle<IGreeter>(instance);

        using var scope = container.CreateScope();

        // Act
        var resolved = scope.GetService<IGreeter>();

        // Assert
        await Assert.That(resolved).IsSameReferenceAs(instance);
    }

    [Test]
    public async Task RegisterSingle_ByInstance_AcrossMultipleScopes()
    {
        // Arrange
        using var container = new SvcContainer();
        var instance = new ConsoleGreeter();

        container.RegisterSingle<IGreeter>(instance);

        // Act
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();
        var resolved1 = scope1.GetService<IGreeter>();
        var resolved2 = scope2.GetService<IGreeter>();

        // Assert
        await Assert.That(resolved1).IsSameReferenceAs(instance);
        await Assert.That(resolved2).IsSameReferenceAs(instance);
    }

    #endregion

    #region Factory Receives Scope

    [Test]
    public async Task Factory_ReceivesScope_CanResolveDependencies()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        container.RegisterTransient<ServiceWithDependency>(
            scope => new ServiceWithDependency(scope.GetService<IGreeter>())
        );

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<ServiceWithDependency>();

        // Assert
        await Assert.That(service).IsNotNull();
        await Assert.That(service.Greeter).IsNotNull();
        await Assert.That(service.Greeter).IsTypeOf<ConsoleGreeter>();
    }

    [Test]
    public async Task Factory_ResolvesMultipleDependencies()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        RegisterConsoleLogger(container);
        container.RegisterTransient<ServiceWithMultipleDependencies>(
            scope =>
                new ServiceWithMultipleDependencies(
                    scope.GetService<IGreeter>(),
                    scope.GetService<ILogger>()
                )
        );

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<ServiceWithMultipleDependencies>();

        // Assert
        await Assert.That(service).IsNotNull();
        await Assert.That(service.Greeter).IsNotNull();
        await Assert.That(service.Logger).IsNotNull();
    }

    #endregion
}
