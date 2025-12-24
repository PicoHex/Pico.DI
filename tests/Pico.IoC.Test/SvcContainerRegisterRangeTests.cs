namespace Pico.IoC.Test;

/// <summary>
/// Tests for RegisterRange method.
/// </summary>
public class SvcContainerRegisterRangeTests : SvcContainerTestBase
{
    [Fact]
    public void RegisterRange_MultipleDescriptors()
    {
        // Arrange
        var container = new SvcContainer();
        var descriptors = new List<SvcDescriptor>
        {
            new(typeof(IGreeter), scope => new ConsoleGreeter(), SvcLifetime.Singleton),
            new(typeof(ILogger), scope => new ConsoleLogger(), SvcLifetime.Singleton)
        };

        // Act
        container.RegisterRange(descriptors);

        // Assert
        using var scope = container.CreateScope();
        var greeter = scope.GetService<IGreeter>();
        var logger = scope.GetService<ILogger>();

        Assert.NotNull(greeter);
        Assert.NotNull(logger);
    }

    [Fact]
    public void RegisterRange_EmptyList()
    {
        // Arrange
        var container = new SvcContainer();

        // Act & Assert
        var result = container.RegisterRange(new List<SvcDescriptor>());
        Assert.NotNull(result);
    }
}
