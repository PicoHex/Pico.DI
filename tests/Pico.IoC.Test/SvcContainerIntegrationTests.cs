namespace Pico.IoC.Test;

/// <summary>
/// Integration tests for fluent API and multiple implementations.
/// </summary>
public class SvcContainerIntegrationTests : SvcContainerTestBase
{
    [Fact]
    public void FluentAPI_ChainMultipleRegistrations()
    {
        // Arrange & Act
        var container = new SvcContainer()
            .RegisterSingleton<IGreeter, ConsoleGreeter>()
            .RegisterSingleton<ILogger, ConsoleLogger>()
            .RegisterTransient<ConsoleGreeter>();

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
        container.RegisterSingleton<IGreeter, ConsoleGreeter>();
        container.RegisterSingleton<IGreeter, AlternativeGreeter>();

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
        container.RegisterSingleton<IGreeter, ConsoleGreeter>();
        container.RegisterSingleton<IGreeter, AlternativeGreeter>();

        // Assert
        using var scope = container.CreateScope();
        var greeters = scope.GetServices<IGreeter>().ToList();

        Assert.Equal(2, greeters.Count);
    }
}
