namespace Pico.DI.Test;

/// <summary>
/// Tests for RegisterTransient methods.
/// Note: Type-based registration methods are placeholder methods scanned by Source Generator.
/// These tests use factory-based registration which actually registers services.
/// </summary>
public class SvcContainerRegisterTransientTests : SvcContainerTestBase
{
    [Fact]
    public void RegisterTransient_ByFactory_NonGeneric_CreatesNewInstanceEachTime()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterTransient(typeof(ConsoleGreeter), _ => new ConsoleGreeter());

        // Assert
        using var scope = container.CreateScope();
        var greeter1 = (ConsoleGreeter)scope.GetService(typeof(ConsoleGreeter));
        var greeter2 = (ConsoleGreeter)scope.GetService(typeof(ConsoleGreeter));

        Assert.NotSame(greeter1, greeter2);
    }

    [Fact]
    public void RegisterTransient_ByFactory_ServiceAndImplementation_NonGeneric()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterTransient(typeof(IGreeter), _ => new ConsoleGreeter());

        // Assert
        using var scope = container.CreateScope();
        var greeter1 = (IGreeter)scope.GetService(typeof(IGreeter));
        var greeter2 = (IGreeter)scope.GetService(typeof(IGreeter));

        Assert.NotSame(greeter1, greeter2);
        Assert.IsType<ConsoleGreeter>(greeter1);
    }

    [Fact]
    public void RegisterTransient_ByFactory_Generic_Single()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterTransient<ConsoleGreeter>(_ => new ConsoleGreeter());

        // Assert
        using var scope = container.CreateScope();
        var greeter1 = scope.GetService<ConsoleGreeter>();
        var greeter2 = scope.GetService<ConsoleGreeter>();

        Assert.NotSame(greeter1, greeter2);
    }

    [Fact]
    public void RegisterTransient_ByFactory_Generic_ServiceAndImplementation()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        RegisterConsoleGreeter(container, SvcLifetime.Transient);

        // Assert
        using var scope = container.CreateScope();
        var greeter1 = scope.GetService<IGreeter>();
        var greeter2 = scope.GetService<IGreeter>();

        Assert.NotSame(greeter1, greeter2);
        Assert.IsType<ConsoleGreeter>(greeter1);
    }

    [Fact]
    public void RegisterTransient_ByFactory_TracksCallCount()
    {
        // Arrange
        var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.RegisterTransient(
            typeof(IGreeter),
            _ =>
            {
                callCount++;
                return new ConsoleGreeter();
            }
        );

        // Assert
        using var scope = container.CreateScope();
        scope.GetService(typeof(IGreeter));
        scope.GetService(typeof(IGreeter));

        Assert.Equal(2, callCount);
    }

    [Fact]
    public void RegisterTransient_ByFactory_Generic_TracksCallCount()
    {
        // Arrange
        var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.RegisterTransient<IGreeter>(_ =>
        {
            callCount++;
            return new ConsoleGreeter();
        });

        // Assert
        using var scope = container.CreateScope();
        scope.GetService<IGreeter>();
        scope.GetService<IGreeter>();

        Assert.Equal(2, callCount);
    }

    [Fact]
    public void RegisterTransient_ByFactory_Generic_ServiceAndImplementation_TracksCallCount()
    {
        // Arrange
        var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.RegisterTransient<IGreeter, ConsoleGreeter>(_ =>
        {
            callCount++;
            return new ConsoleGreeter();
        });

        // Assert
        using var scope = container.CreateScope();
        scope.GetService<IGreeter>();
        scope.GetService<IGreeter>();

        Assert.Equal(2, callCount);
    }
}
