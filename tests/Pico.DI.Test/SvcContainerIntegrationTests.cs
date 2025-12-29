namespace Pico.DI.Test;

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
    public void TypeBasedRegistration_WithAotSafeActivator_Works()
    {
        // Arrange - Type-based registration uses AOT-safe Activator.CreateInstance<T>()
        // which is compatible with Native AOT (type parameter is compile-time known)
        var container = new SvcContainer()
            .RegisterSingleton<IGreeter>(_ => new ConsoleGreeter())
            .RegisterSingleton<ILogger>(_ => new ConsoleLogger());

        // Assert - Services are registered with AOT-safe factory
        using var scope = container.CreateScope();

        var greeter = scope.GetService<IGreeter>();
        var logger = scope.GetService<ILogger>();

        Assert.NotNull(greeter);
        Assert.NotNull(logger);
    }

    [Fact]
    public void MixedRegistration_TypeAndFactory_BothWork()
    {
        // Arrange
        var container = new SvcContainer()
            .RegisterSingleton<IGreeter>(_ => new ConsoleGreeter()) // Factory-based registration
            .RegisterSingleton<ILogger>(_ => new ConsoleLogger()); // Factory-based

        // Assert
        using var scope = container.CreateScope();

        var greeter = scope.GetService<IGreeter>();
        Assert.NotNull(greeter);
        Assert.IsType<ConsoleGreeter>(greeter);

        var logger = scope.GetService<ILogger>();
        Assert.NotNull(logger);
        Assert.IsType<ConsoleLogger>(logger);
    }
}
