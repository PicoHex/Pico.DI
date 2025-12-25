namespace Pico.IoC.Test;

/// <summary>
/// Tests for Register by Type with Lifetime methods.
/// Note: Type-based registration methods are placeholder methods scanned by Source Generator.
/// These tests verify the placeholder behavior (returning container without registering).
/// </summary>
public class SvcContainerRegisterByTypeTests : SvcContainerTestBase
{
    [Fact]
    public void Register_ByType_ReturnsContainerForChaining()
    {
        // Arrange
        var container = new SvcContainer();

        // Act - Type-based registration returns container for chaining
        var result = container.Register(typeof(ConsoleGreeter), SvcLifetime.Singleton);

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void Register_ByType_DoesNotActuallyRegister()
    {
        // Arrange
        var container = new SvcContainer();

        // Act - Type-based registration is a placeholder, doesn't register
        container.Register(typeof(ConsoleGreeter), SvcLifetime.Singleton);

        // Assert - Service should not be registered
        using var scope = container.CreateScope();
        Assert.Throws<PicoIocException>(() => scope.GetService(typeof(ConsoleGreeter)));
    }

    [Fact]
    public void RegisterGeneric_ByType_ReturnsContainerForChaining()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.Register<ConsoleGreeter>(SvcLifetime.Singleton);

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterGeneric_ServiceAndImplementation_ReturnsContainerForChaining()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.Register<IGreeter, ConsoleGreeter>(SvcLifetime.Singleton);

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterTransient_ByType_ReturnsContainerForChaining()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterTransient<ConsoleGreeter>();

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterScoped_ByType_ReturnsContainerForChaining()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterScoped<ConsoleGreeter>();

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterSingleton_ByType_ReturnsContainerForChaining()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterSingleton<ConsoleGreeter>();

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void ChainedTypeRegistrations_AllReturnContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act - Chain multiple type-based registrations (all placeholders)
        var result = container
            .RegisterSingleton<ConsoleGreeter>()
            .RegisterTransient<AlternativeGreeter>()
            .RegisterScoped<ConsoleLogger>();

        // Assert
        Assert.Same(container, result);
    }

    #region Additional Type-based Registration Coverage Tests

    [Fact]
    public void Register_ServiceAndImplementationType_WithLifetime_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.Register(
            typeof(IGreeter),
            typeof(ConsoleGreeter),
            SvcLifetime.Transient
        );

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterGeneric_WithImplementationType_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.Register<IGreeter>(typeof(ConsoleGreeter), SvcLifetime.Singleton);

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterTransient_NonGeneric_ServiceType_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterTransient(typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterTransient_NonGeneric_ServiceAndImplementationType_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterTransient(typeof(IGreeter), typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterTransient_Generic_WithImplementationType_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterTransient<IGreeter>(typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterTransient_Generic_ServiceAndImplementation_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterTransient<IGreeter, ConsoleGreeter>();

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterScoped_NonGeneric_ServiceType_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterScoped(typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterScoped_NonGeneric_ServiceAndImplementationType_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterScoped(typeof(IGreeter), typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterScoped_Generic_WithImplementationType_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterScoped<IGreeter>(typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterScoped_Generic_ServiceAndImplementation_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterScoped<IGreeter, ConsoleGreeter>();

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterSingleton_NonGeneric_ServiceType_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterSingleton(typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterSingleton_NonGeneric_ServiceAndImplementationType_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterSingleton(typeof(IGreeter), typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterSingleton_Generic_WithImplementationType_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterSingleton<IGreeter>(typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterSingleton_Generic_ServiceAndImplementation_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterSingleton<IGreeter, ConsoleGreeter>();

        // Assert
        Assert.Same(container, result);
    }

    #endregion
}
