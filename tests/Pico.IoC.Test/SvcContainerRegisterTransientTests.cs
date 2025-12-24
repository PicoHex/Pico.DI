namespace Pico.IoC.Test;

/// <summary>
/// Tests for RegisterTransient methods.
/// </summary>
public class SvcContainerRegisterTransientTests : SvcContainerTestBase
{
    [Fact]
    public void RegisterTransient_ByType_NonGeneric()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterTransient(typeof(ConsoleGreeter));

        // Assert
        using var scope = container.CreateScope();
        var greeter1 = (ConsoleGreeter)scope.GetService(typeof(ConsoleGreeter));
        var greeter2 = (ConsoleGreeter)scope.GetService(typeof(ConsoleGreeter));

        Assert.NotSame(greeter1, greeter2);
    }

    [Fact]
    public void RegisterTransient_ServiceAndImplementation_NonGeneric()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterTransient(typeof(IGreeter), typeof(ConsoleGreeter));

        // Assert
        using var scope = container.CreateScope();
        var greeter1 = (IGreeter)scope.GetService(typeof(IGreeter));
        var greeter2 = (IGreeter)scope.GetService(typeof(IGreeter));

        Assert.NotSame(greeter1, greeter2);
        Assert.IsType<ConsoleGreeter>(greeter1);
    }

    [Fact]
    public void RegisterTransient_Generic_Single()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterTransient<ConsoleGreeter>();

        // Assert
        using var scope = container.CreateScope();
        var greeter1 = scope.GetService<ConsoleGreeter>();
        var greeter2 = scope.GetService<ConsoleGreeter>();

        Assert.NotSame(greeter1, greeter2);
    }

    [Fact]
    public void RegisterTransient_Generic_ServiceAndImplementation()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterTransient<IGreeter, ConsoleGreeter>();

        // Assert
        using var scope = container.CreateScope();
        var greeter1 = scope.GetService<IGreeter>();
        var greeter2 = scope.GetService<IGreeter>();

        Assert.NotSame(greeter1, greeter2);
        Assert.IsType<ConsoleGreeter>(greeter1);
    }

    [Fact]
    public void RegisterTransient_Generic_ServiceAndImplementationType()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterTransient<IGreeter>(typeof(ConsoleGreeter));

        // Assert
        using var scope = container.CreateScope();
        var greeter = scope.GetService<IGreeter>();

        Assert.NotNull(greeter);
        Assert.IsType<ConsoleGreeter>(greeter);
    }

    [Fact]
    public void RegisterTransient_ByFactory_NonGeneric()
    {
        // Arrange
        var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.RegisterTransient(
            typeof(IGreeter),
            scope =>
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
    public void RegisterTransient_ByFactory_Generic()
    {
        // Arrange
        var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.RegisterTransient<IGreeter>(scope =>
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
    public void RegisterTransient_ByFactory_Generic_ServiceAndImplementation()
    {
        // Arrange
        var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.RegisterTransient<IGreeter, ConsoleGreeter>(scope =>
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
