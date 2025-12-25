namespace Pico.IoC.Test;

/// <summary>
/// Integration tests for fluent API and multiple implementations.
/// </summary>
public class SvcContainerIntegrationTests : SvcContainerTestBase
{
    [Fact]
    public void FluentAPI_ChainMultipleFactoryRegistrations()
    {
        // Arrange & Act
        var container = new SvcContainer()
            .RegisterSingleton<IGreeter>(_ => new ConsoleGreeter())
            .RegisterSingleton<ILogger>(_ => new ConsoleLogger())
            .RegisterTransient<ConsoleGreeter>(_ => new ConsoleGreeter());

        // Assert
        Assert.NotNull(container);

        using var scope = container.CreateScope();
        var greeter = scope.GetService<IGreeter>();
        var logger = scope.GetService<ILogger>();

        Assert.NotNull(greeter);
        Assert.NotNull(logger);
    }

    [Fact]
    public void MultipleImplementations_LastOneReturned()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterSingleton<IGreeter>(_ => new ConsoleGreeter());
        container.RegisterSingleton<IGreeter>(_ => new AlternativeGreeter());

        // Assert
        using var scope = container.CreateScope();
        var greeter = scope.GetService<IGreeter>();

        Assert.IsType<AlternativeGreeter>(greeter);
    }

    [Fact]
    public void MultipleImplementations_GetServices_ReturnsAll()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterSingleton<IGreeter>(_ => new ConsoleGreeter());
        container.RegisterSingleton<IGreeter>(_ => new AlternativeGreeter());

        // Assert
        using var scope = container.CreateScope();
        var greeters = scope.GetServices<IGreeter>().ToList();

        Assert.Equal(2, greeters.Count);
    }

    [Fact]
    public void TypeBasedRegistration_IsPlaceholder_NoActualRegistration()
    {
        // Arrange - Using type-based registration (placeholder for Source Generator)
        var container = new SvcContainer()
            .RegisterSingleton<IGreeter, ConsoleGreeter>() // This is a placeholder
            .RegisterSingleton<ILogger, ConsoleLogger>(); // This is a placeholder

        // Assert - Services are not actually registered
        using var scope = container.CreateScope();

        // These should throw because type-based registration doesn't register
        Assert.Throws<PicoIocException>(() => scope.GetService<IGreeter>());
        Assert.Throws<PicoIocException>(() => scope.GetService<ILogger>());
    }

    [Fact]
    public void MixedRegistration_FactoryWorks_TypeIsPlaceholder()
    {
        // Arrange
        var container = new SvcContainer()
            .RegisterSingleton<IGreeter, ConsoleGreeter>() // Placeholder (no registration)
            .RegisterSingleton<ILogger>(_ => new ConsoleLogger()); // Factory (actual registration)

        // Assert
        using var scope = container.CreateScope();

        // Type-based registration is placeholder - throws
        Assert.Throws<PicoIocException>(() => scope.GetService<IGreeter>());

        // Factory-based registration works
        var logger = scope.GetService<ILogger>();
        Assert.NotNull(logger);
        Assert.IsType<ConsoleLogger>(logger);
    }
}
