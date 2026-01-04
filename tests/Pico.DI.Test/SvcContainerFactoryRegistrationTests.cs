namespace Pico.DI.Test;

/// <summary>
/// Tests for factory registration methods.
/// </summary>
public class SvcContainerFactoryRegistrationTests : XUnitTestBase
{
    #region RegisterTransient Factory Tests

    [Fact]
    public void RegisterTransient_WithGenericFactory_ResolvesCorrectly()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IGreeter>();

        // Assert
        Assert.IsType<ConsoleGreeter>(service);
    }

    [Fact]
    public void RegisterTransient_WithTypeAndFactory_ResolvesCorrectly()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterTransient(typeof(IGreeter), _ => new ConsoleGreeter());

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IGreeter>();

        // Assert
        Assert.IsType<ConsoleGreeter>(service);
    }

    [Fact]
    public void RegisterTransient_WithTwoGenericsFactory_ResolvesCorrectly()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterTransient<IGreeter, ConsoleGreeter>(_ => new ConsoleGreeter());

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IGreeter>();

        // Assert
        Assert.IsType<ConsoleGreeter>(service);
    }

    [Fact]
    public void RegisterTransient_ReturnsContainer_ForChaining()
    {
        // Arrange
        using var container = CreateContainer();

        // Act
        var result = container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());

        // Assert
        Assert.Same(container, result);
    }

    #endregion

    #region RegisterScoped Factory Tests

    [Fact]
    public void RegisterScoped_WithGenericFactory_ResolvesCorrectly()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterScoped<IGreeter>(_ => new ConsoleGreeter());

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IGreeter>();

        // Assert
        Assert.IsType<ConsoleGreeter>(service);
    }

    [Fact]
    public void RegisterScoped_WithTypeAndFactory_ResolvesCorrectly()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterScoped(typeof(IGreeter), _ => new ConsoleGreeter());

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IGreeter>();

        // Assert
        Assert.IsType<ConsoleGreeter>(service);
    }

    [Fact]
    public void RegisterScoped_WithTwoGenericsFactory_ResolvesCorrectly()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterScoped<IGreeter, ConsoleGreeter>(_ => new ConsoleGreeter());

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IGreeter>();

        // Assert
        Assert.IsType<ConsoleGreeter>(service);
    }

    [Fact]
    public void RegisterScoped_ReturnsContainer_ForChaining()
    {
        // Arrange
        using var container = CreateContainer();

        // Act
        var result = container.RegisterScoped<IGreeter>(_ => new ConsoleGreeter());

        // Assert
        Assert.Same(container, result);
    }

    #endregion

    #region RegisterSingleton Factory Tests

    [Fact]
    public void RegisterSingleton_WithGenericFactory_ResolvesCorrectly()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterSingleton<IGreeter>(_ => new ConsoleGreeter());

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IGreeter>();

        // Assert
        Assert.IsType<ConsoleGreeter>(service);
    }

    [Fact]
    public void RegisterSingleton_WithTypeAndFactory_ResolvesCorrectly()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterSingleton(typeof(IGreeter), _ => new ConsoleGreeter());

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IGreeter>();

        // Assert
        Assert.IsType<ConsoleGreeter>(service);
    }

    [Fact]
    public void RegisterSingleton_WithTwoGenericsFactory_ResolvesCorrectly()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterSingleton<IGreeter, ConsoleGreeter>(_ => new ConsoleGreeter());

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IGreeter>();

        // Assert
        Assert.IsType<ConsoleGreeter>(service);
    }

    [Fact]
    public void RegisterSingleton_ReturnsContainer_ForChaining()
    {
        // Arrange
        using var container = CreateContainer();

        // Act
        var result = container.RegisterSingleton<IGreeter>(_ => new ConsoleGreeter());

        // Assert
        Assert.Same(container, result);
    }

    #endregion

    #region Register with Lifetime Parameter Tests

    [Fact]
    public void Register_WithFactory_AndLifetime_Transient()
    {
        // Arrange
        using var container = CreateContainer();
        container.Register<IGreeter>(_ => new ConsoleGreeter(), SvcLifetime.Transient);

        using var scope = container.CreateScope();

        // Act
        var s1 = scope.GetService<IGreeter>();
        var s2 = scope.GetService<IGreeter>();

        // Assert
        Assert.NotSame(s1, s2);
    }

    [Fact]
    public void Register_WithFactory_AndLifetime_Scoped()
    {
        // Arrange
        using var container = CreateContainer();
        container.Register<IGreeter>(_ => new ConsoleGreeter(), SvcLifetime.Scoped);

        using var scope = container.CreateScope();

        // Act
        var s1 = scope.GetService<IGreeter>();
        var s2 = scope.GetService<IGreeter>();

        // Assert
        Assert.Same(s1, s2);
    }

    [Fact]
    public void Register_WithFactory_AndLifetime_Singleton()
    {
        // Arrange
        using var container = CreateContainer();
        container.Register<IGreeter>(_ => new ConsoleGreeter(), SvcLifetime.Singleton);

        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        // Act
        var s1 = scope1.GetService<IGreeter>();
        var s2 = scope2.GetService<IGreeter>();

        // Assert
        Assert.Same(s1, s2);
    }

    [Fact]
    public void Register_TypeWithFactory_AndLifetime()
    {
        // Arrange
        using var container = CreateContainer();
        container.Register(typeof(IGreeter), _ => new ConsoleGreeter(), SvcLifetime.Transient);

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IGreeter>();

        // Assert
        Assert.IsType<ConsoleGreeter>(service);
    }

    [Fact]
    public void Register_TwoGenericsWithFactory_AndLifetime()
    {
        // Arrange
        using var container = CreateContainer();
        container.Register<IGreeter, ConsoleGreeter>(
            _ => new ConsoleGreeter(),
            SvcLifetime.Singleton
        );

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IGreeter>();

        // Assert
        Assert.IsType<ConsoleGreeter>(service);
    }

    #endregion

    #region Factory with Dependencies Tests

    [Fact]
    public void Factory_CanResolve_Dependencies()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());
        container.RegisterTransient<ILogger>(_ => new ConsoleLogger());
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
        Assert.NotNull(service);
        Assert.NotNull(service.Greeter);
        Assert.NotNull(service.Logger);
    }

    [Fact]
    public void Factory_CanMix_Lifetimes()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterSingleton<IGreeter>(_ => new ConsoleGreeter());
        container.RegisterScoped<ILogger>(_ => new ConsoleLogger());
        container.RegisterTransient<ServiceWithMultipleDependencies>(
            scope =>
                new ServiceWithMultipleDependencies(
                    scope.GetService<IGreeter>(),
                    scope.GetService<ILogger>()
                )
        );

        // Act
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        var s1 = scope1.GetService<ServiceWithMultipleDependencies>();
        var s2 = scope2.GetService<ServiceWithMultipleDependencies>();

        // Assert
        Assert.Same(s1.Greeter, s2.Greeter); // Singleton - same instance
        Assert.NotSame(s1.Logger, s2.Logger); // Scoped - different per scope
        Assert.NotSame(s1, s2); // Transient - different instances
    }

    #endregion
}
