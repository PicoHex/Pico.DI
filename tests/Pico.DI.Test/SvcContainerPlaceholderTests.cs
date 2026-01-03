namespace Pico.DI.Test;

/// <summary>
/// Tests for placeholder methods that are scanned by source generator.
/// These methods do nothing at runtime but are important for source generator detection.
/// </summary>
public class SvcContainerPlaceholderTests : XUnitTestBase
{
    #region RegisterTransient Placeholder Tests

    [Fact]
    public void RegisterTransient_Generic_TwoTypes_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - placeholder method returns container unchanged
        var result = container.RegisterTransient<IGreeter, ConsoleGreeter>();

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterTransient_Generic_SingleType_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - placeholder method returns container unchanged
        var result = container.RegisterTransient<ConsoleGreeter>();

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterTransient_Generic_WithImplementationType_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - placeholder method returns container unchanged
        var result = container.RegisterTransient<IGreeter>(typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
    }

    #endregion

    #region RegisterScoped Placeholder Tests

    [Fact]
    public void RegisterScoped_Generic_TwoTypes_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - placeholder method returns container unchanged
        var result = container.RegisterScoped<IGreeter, ConsoleGreeter>();

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterScoped_Generic_SingleType_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - placeholder method returns container unchanged
        var result = container.RegisterScoped<ConsoleGreeter>();

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterScoped_Generic_WithImplementationType_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - placeholder method returns container unchanged
        var result = container.RegisterScoped<IGreeter>(typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
    }

    #endregion

    #region RegisterSingleton Placeholder Tests

    [Fact]
    public void RegisterSingleton_Generic_TwoTypes_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - placeholder method returns container unchanged
        var result = container.RegisterSingleton<IGreeter, ConsoleGreeter>();

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterSingleton_Generic_SingleType_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - placeholder method returns container unchanged
        var result = container.RegisterSingleton<ConsoleGreeter>();

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterSingleton_Generic_WithImplementationType_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - placeholder method returns container unchanged
        var result = container.RegisterSingleton<IGreeter>(typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
    }

    #endregion

    #region Register with Lifetime Placeholder Tests

    [Fact]
    public void Register_Generic_TwoTypes_WithLifetime_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - placeholder method returns container unchanged
        var result = container.Register<IGreeter, ConsoleGreeter>(SvcLifetime.Transient);

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void Register_Generic_SingleType_WithLifetime_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - placeholder method returns container unchanged
        var result = container.Register<ConsoleGreeter>(SvcLifetime.Scoped);

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void Register_Generic_WithImplementationType_AndLifetime_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - placeholder method returns container unchanged
        var result = container.Register<IGreeter>(typeof(ConsoleGreeter), SvcLifetime.Singleton);

        // Assert
        Assert.Same(container, result);
    }

    #endregion

    #region Placeholder Does Not Register Tests

    [Fact]
    public void RegisterTransient_Placeholder_DoesNotActuallyRegister()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - placeholder method does nothing
        container.RegisterTransient<IGreeter, ConsoleGreeter>();

        using var scope = container.CreateScope();

        // Assert - service is not registered
        Assert.Throws<PicoDiException>(() => scope.GetService<IGreeter>());
    }

    [Fact]
    public void RegisterScoped_Placeholder_DoesNotActuallyRegister()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - placeholder method does nothing
        container.RegisterScoped<IGreeter, ConsoleGreeter>();

        using var scope = container.CreateScope();

        // Assert - service is not registered
        Assert.Throws<PicoDiException>(() => scope.GetService<IGreeter>());
    }

    [Fact]
    public void RegisterSingleton_Placeholder_DoesNotActuallyRegister()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - placeholder method does nothing
        container.RegisterSingleton<IGreeter, ConsoleGreeter>();

        using var scope = container.CreateScope();

        // Assert - service is not registered
        Assert.Throws<PicoDiException>(() => scope.GetService<IGreeter>());
    }

    #endregion

    #region Chaining Placeholder Tests

    [Fact]
    public void Placeholders_CanBeChained()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - chain multiple placeholders
        var result = container
            .RegisterTransient<IGreeter, ConsoleGreeter>()
            .RegisterScoped<ILogger, ConsoleLogger>()
            .RegisterSingleton<CountingService>();

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void Placeholders_CanBeMixedWithRealRegistrations()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - mix placeholders with real factory registrations
        var result = container
            .RegisterTransient<IGreeter, ConsoleGreeter>() // Placeholder - does nothing
            .RegisterTransient<IGreeter>(_ => new ConsoleGreeter()) // Real registration
            .RegisterSingleton<ILogger, ConsoleLogger>(); // Placeholder - does nothing

        using var scope = container.CreateScope();

        // Assert - only factory registration works
        Assert.NotNull(scope.GetService<IGreeter>());
        Assert.Throws<PicoDiException>(() => scope.GetService<ILogger>());
    }

    #endregion
}
