namespace Pico.IoC.Test;

/// <summary>
/// Tests for RegisterSingleton methods.
/// </summary>
public class SvcContainerRegisterSingletonTests : SvcContainerTestBase
{
    [Fact]
    public void RegisterSingleton_ByType_NonGeneric()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterSingleton(typeof(ConsoleGreeter));

        // Assert
        using var scope1 = container.CreateScope();
        var greeter1 = (ConsoleGreeter)scope1.GetService(typeof(ConsoleGreeter));

        using var scope2 = container.CreateScope();
        var greeter2 = (ConsoleGreeter)scope2.GetService(typeof(ConsoleGreeter));

        Assert.Same(greeter1, greeter2);
    }

    [Fact]
    public void RegisterSingleton_ServiceAndImplementation_NonGeneric()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterSingleton(typeof(IGreeter), typeof(ConsoleGreeter));

        // Assert
        using var scope1 = container.CreateScope();
        var greeter1 = (IGreeter)scope1.GetService(typeof(IGreeter));

        using var scope2 = container.CreateScope();
        var greeter2 = (IGreeter)scope2.GetService(typeof(IGreeter));

        Assert.Same(greeter1, greeter2);
        Assert.IsType<ConsoleGreeter>(greeter1);
    }

    [Fact]
    public void RegisterSingleton_Generic_Single()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterSingleton<ConsoleGreeter>();

        // Assert
        using var scope1 = container.CreateScope();
        var greeter1 = scope1.GetService<ConsoleGreeter>();

        using var scope2 = container.CreateScope();
        var greeter2 = scope2.GetService<ConsoleGreeter>();

        Assert.Same(greeter1, greeter2);
    }

    [Fact]
    public void RegisterSingleton_Generic_ServiceAndImplementation()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterSingleton<IGreeter, ConsoleGreeter>();

        // Assert
        using var scope1 = container.CreateScope();
        var greeter1 = scope1.GetService<IGreeter>();

        using var scope2 = container.CreateScope();
        var greeter2 = scope2.GetService<IGreeter>();

        Assert.Same(greeter1, greeter2);
        Assert.IsType<ConsoleGreeter>(greeter1);
    }

    [Fact]
    public void RegisterSingleton_Generic_ServiceAndImplementationType()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterSingleton<IGreeter>(typeof(ConsoleGreeter));

        // Assert
        using var scope = container.CreateScope();
        var greeter = scope.GetService<IGreeter>();

        Assert.NotNull(greeter);
        Assert.IsType<ConsoleGreeter>(greeter);
    }

    [Fact]
    public void RegisterSingleton_ByFactory_NonGeneric()
    {
        // Arrange
        var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.RegisterSingleton(
            typeof(IGreeter),
            scope =>
            {
                callCount++;
                return new ConsoleGreeter();
            }
        );

        // Assert
        using var scope1 = container.CreateScope();
        scope1.GetService(typeof(IGreeter));

        using var scope2 = container.CreateScope();
        scope2.GetService(typeof(IGreeter));

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void RegisterSingleton_ByFactory_Generic()
    {
        // Arrange
        var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.RegisterSingleton<IGreeter>(scope =>
        {
            callCount++;
            return new ConsoleGreeter();
        });

        // Assert
        using var scope1 = container.CreateScope();
        scope1.GetService<IGreeter>();

        using var scope2 = container.CreateScope();
        scope2.GetService<IGreeter>();

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void RegisterSingleton_ByFactory_Generic_ServiceAndImplementation()
    {
        // Arrange
        var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.RegisterSingleton<IGreeter, ConsoleGreeter>(scope =>
        {
            callCount++;
            return new ConsoleGreeter();
        });

        // Assert
        using var scope1 = container.CreateScope();
        scope1.GetService<IGreeter>();

        using var scope2 = container.CreateScope();
        scope2.GetService<IGreeter>();

        Assert.Equal(1, callCount);
    }
}
