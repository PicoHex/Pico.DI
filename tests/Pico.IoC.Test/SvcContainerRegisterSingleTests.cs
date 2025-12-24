namespace Pico.IoC.Test;

/// <summary>
/// Tests for RegisterSingle (instance registration) methods.
/// </summary>
public class SvcContainerRegisterSingleTests : SvcContainerTestBase
{
    [Fact]
    public void RegisterSingle_NonGeneric()
    {
        // Arrange
        var container = new SvcContainer();
        var instance = new ConsoleGreeter();

        // Act
        container.RegisterSingle(typeof(IGreeter), instance);

        // Assert
        using var scope1 = container.CreateScope();
        var greeter1 = (IGreeter)scope1.GetService(typeof(IGreeter));

        using var scope2 = container.CreateScope();
        var greeter2 = (IGreeter)scope2.GetService(typeof(IGreeter));

        Assert.Same(instance, greeter1);
        Assert.Same(greeter1, greeter2);
    }

    [Fact]
    public void RegisterSingle_Generic()
    {
        // Arrange
        var container = new SvcContainer();
        var instance = new ConsoleGreeter();

        // Act
        container.RegisterSingle<IGreeter>(instance);

        // Assert
        using var scope1 = container.CreateScope();
        var greeter1 = scope1.GetService<IGreeter>();

        using var scope2 = container.CreateScope();
        var greeter2 = scope2.GetService<IGreeter>();

        Assert.Same(instance, greeter1);
        Assert.Same(greeter1, greeter2);
    }
}
