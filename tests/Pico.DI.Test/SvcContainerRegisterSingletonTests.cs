namespace Pico.DI.Test;

/// <summary>
/// Tests for RegisterSingleton methods.
/// Note: Type-based registration methods are placeholder methods scanned by Source Generator.
/// These tests use factory-based registration which actually registers services.
/// </summary>
public class SvcContainerRegisterSingletonTests : SvcContainerTestBase
{
    [Fact]
    public void RegisterSingleton_ByFactory_NonGeneric_SameInstanceAcrossScopes()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterSingleton(typeof(ConsoleGreeter), _ => new ConsoleGreeter());

        // Assert
        using var scope1 = container.CreateScope();
        var greeter1 = (ConsoleGreeter)scope1.GetService(typeof(ConsoleGreeter));

        using var scope2 = container.CreateScope();
        var greeter2 = (ConsoleGreeter)scope2.GetService(typeof(ConsoleGreeter));

        Assert.Same(greeter1, greeter2);
    }

    [Fact]
    public void RegisterSingleton_ByFactory_ServiceAndImplementation_NonGeneric()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterSingleton(typeof(IGreeter), _ => new ConsoleGreeter());

        // Assert
        using var scope1 = container.CreateScope();
        var greeter1 = (IGreeter)scope1.GetService(typeof(IGreeter));

        using var scope2 = container.CreateScope();
        var greeter2 = (IGreeter)scope2.GetService(typeof(IGreeter));

        Assert.Same(greeter1, greeter2);
        Assert.IsType<ConsoleGreeter>(greeter1);
    }

    [Fact]
    public void RegisterSingleton_ByFactory_Generic_Single()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterSingleton<ConsoleGreeter>(_ => new ConsoleGreeter());

        // Assert
        using var scope1 = container.CreateScope();
        var greeter1 = scope1.GetService<ConsoleGreeter>();

        using var scope2 = container.CreateScope();
        var greeter2 = scope2.GetService<ConsoleGreeter>();

        Assert.Same(greeter1, greeter2);
    }

    [Fact]
    public void RegisterSingleton_ByFactory_Generic_ServiceAndImplementation()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterSingleton<IGreeter>(_ => new ConsoleGreeter());

        // Assert
        using var scope1 = container.CreateScope();
        var greeter1 = scope1.GetService<IGreeter>();

        using var scope2 = container.CreateScope();
        var greeter2 = scope2.GetService<IGreeter>();

        Assert.Same(greeter1, greeter2);
        Assert.IsType<ConsoleGreeter>(greeter1);
    }

    [Fact]
    public void RegisterSingleton_ByFactory_NonGeneric_TracksCallCount()
    {
        // Arrange
        var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.RegisterSingleton(
            typeof(IGreeter),
            _ =>
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

        Assert.Equal(1, callCount); // Only called once globally
    }

    [Fact]
    public void RegisterSingleton_ByFactory_Generic_TracksCallCount()
    {
        // Arrange
        var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.RegisterSingleton<IGreeter>(_ =>
        {
            callCount++;
            return new ConsoleGreeter();
        });

        // Assert
        using var scope1 = container.CreateScope();
        scope1.GetService<IGreeter>();

        using var scope2 = container.CreateScope();
        scope2.GetService<IGreeter>();

        Assert.Equal(1, callCount); // Only called once globally
    }

    [Fact]
    public void RegisterSingleton_ByFactory_Generic_ServiceAndImplementation_TracksCallCount()
    {
        // Arrange
        var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.RegisterSingleton<IGreeter, ConsoleGreeter>(_ =>
        {
            callCount++;
            return new ConsoleGreeter();
        });

        // Assert
        using var scope1 = container.CreateScope();
        scope1.GetService<IGreeter>();

        using var scope2 = container.CreateScope();
        scope2.GetService<IGreeter>();

        Assert.Equal(1, callCount); // Only called once globally
    }
}
