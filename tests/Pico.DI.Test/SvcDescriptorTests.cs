namespace Pico.DI.Test;

/// <summary>
/// Tests for SvcDescriptor class.
/// </summary>
public class SvcDescriptorTests : XUnitTestBase
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithTypeAndLifetime_SetsProperties()
    {
        // Act
        var descriptor = new SvcDescriptor(
            typeof(IGreeter),
            typeof(ConsoleGreeter),
            SvcLifetime.Transient
        );

        // Assert
        Assert.Equal(typeof(IGreeter), descriptor.ServiceType);
        Assert.Equal(typeof(ConsoleGreeter), descriptor.ImplementationType);
        Assert.Equal(SvcLifetime.Transient, descriptor.Lifetime);
        Assert.Null(descriptor.Factory);
        Assert.Null(descriptor.SingleInstance);
    }

    [Fact]
    public void Constructor_WithServiceTypeOnly_ImplementationTypeMatchesService()
    {
        // Act
        var descriptor = new SvcDescriptor(
            typeof(ConsoleGreeter),
            (Type?)null,
            SvcLifetime.Singleton
        );

        // Assert
        Assert.Equal(typeof(ConsoleGreeter), descriptor.ServiceType);
        Assert.Equal(typeof(ConsoleGreeter), descriptor.ImplementationType);
    }

    [Fact]
    public void Constructor_WithFactory_SetsFactory()
    {
        // Arrange
        Func<ISvcScope, object> factory = _ => new ConsoleGreeter();

        // Act
        var descriptor = new SvcDescriptor(typeof(IGreeter), factory, SvcLifetime.Transient);

        // Assert
        Assert.Same(factory, descriptor.Factory);
        Assert.Equal(typeof(IGreeter), descriptor.ServiceType);
        Assert.Equal(SvcLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void Constructor_WithInstance_SetsSingleInstance()
    {
        // Arrange
        var instance = new ConsoleGreeter();

        // Act
        var descriptor = new SvcDescriptor(typeof(IGreeter), instance);

        // Assert
        Assert.Same(instance, descriptor.SingleInstance);
        Assert.Equal(typeof(IGreeter), descriptor.ServiceType);
        Assert.Equal(SvcLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void Constructor_WithNullServiceType_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new SvcDescriptor(null!, typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void Constructor_WithNullFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () =>
                new SvcDescriptor(
                    typeof(IGreeter),
                    (Func<ISvcScope, object>)null!,
                    SvcLifetime.Transient
                )
        );
    }

    [Fact]
    public void Constructor_WithNullInstance_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new SvcDescriptor(typeof(IGreeter), (object)null!)
        );
    }

    #endregion

    #region Lifetime Tests

    [Theory]
    [InlineData(SvcLifetime.Transient)]
    [InlineData(SvcLifetime.Scoped)]
    [InlineData(SvcLifetime.Singleton)]
    public void Constructor_AllLifetimes_WorkCorrectly(SvcLifetime lifetime)
    {
        // Act
        var descriptor = new SvcDescriptor(typeof(IGreeter), typeof(ConsoleGreeter), lifetime);

        // Assert
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    [Fact]
    public void DefaultLifetime_IsSingleton()
    {
        // Act
        var descriptor = new SvcDescriptor(typeof(IGreeter), typeof(ConsoleGreeter));

        // Assert
        Assert.Equal(SvcLifetime.Singleton, descriptor.Lifetime);
    }

    #endregion

    #region Factory Execution Tests

    [Fact]
    public void Factory_WhenCalled_ReturnsExpectedInstance()
    {
        // Arrange
        var expectedGreeter = new ConsoleGreeter();
        var descriptor = new SvcDescriptor(
            typeof(IGreeter),
            _ => expectedGreeter,
            SvcLifetime.Transient
        );

        // Act
        using var container = new SvcContainer();
        container.Register(descriptor);
        using var scope = container.CreateScope();
        var result = scope.GetService<IGreeter>();

        // Assert
        Assert.Same(expectedGreeter, result);
    }

    [Fact]
    public void Factory_ReceivesScopeParameter_CanResolveDependencies()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());
        container.RegisterTransient<ServiceWithDependency>(
            scope => new ServiceWithDependency(scope.GetService<IGreeter>())
        );

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<ServiceWithDependency>();

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.Greeter);
        Assert.IsType<ConsoleGreeter>(service.Greeter);
    }

    #endregion

    #region SingleInstance Tests

    [Fact]
    public void SingleInstance_CanBeModified_AfterConstruction()
    {
        // Arrange
        var descriptor = new SvcDescriptor(
            typeof(IGreeter),
            typeof(ConsoleGreeter),
            SvcLifetime.Singleton
        );

        // Act
        var instance = new ConsoleGreeter();
        descriptor.SingleInstance = instance;

        // Assert
        Assert.Same(instance, descriptor.SingleInstance);
    }

    #endregion
}
