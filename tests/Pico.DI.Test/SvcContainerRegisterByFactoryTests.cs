namespace Pico.DI.Test;

/// <summary>
/// Tests for Register by Factory methods.
/// </summary>
public class SvcContainerRegisterByFactoryTests : SvcContainerTestBase
{
    [Fact]
    public void Register_ByFactory_NonGeneric_WithLifetime()
    {
        // Arrange
        var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.Register(
            typeof(IGreeter),
            scope =>
            {
                callCount++;
                return new ConsoleGreeter();
            },
            SvcLifetime.Transient
        );

        // Assert
        using var scope = container.CreateScope();
        scope.GetService(typeof(IGreeter));
        scope.GetService(typeof(IGreeter));

        Assert.Equal(2, callCount);
    }

    [Fact]
    public void Register_ByFactory_Generic_WithLifetime()
    {
        // Arrange
        var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.Register<IGreeter>(
            scope =>
            {
                callCount++;
                return new ConsoleGreeter();
            },
            SvcLifetime.Transient
        );

        // Assert
        using var scope = container.CreateScope();
        scope.GetService<IGreeter>();
        scope.GetService<IGreeter>();

        Assert.Equal(2, callCount);
    }

    [Fact]
    public void Register_ByFactory_Generic_ServiceAndImplementation_WithLifetime()
    {
        // Arrange
        var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.Register<IGreeter, ConsoleGreeter>(
            scope =>
            {
                callCount++;
                return new ConsoleGreeter();
            },
            SvcLifetime.Transient
        );

        // Assert
        using var scope = container.CreateScope();
        scope.GetService<IGreeter>();
        scope.GetService<IGreeter>();

        Assert.Equal(2, callCount);
    }
}
