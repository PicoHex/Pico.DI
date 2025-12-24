namespace Pico.IoC.Test;

/// <summary>
/// Tests for RegisterScoped methods.
/// </summary>
public class SvcContainerRegisterScopedTests : SvcContainerTestBase
{
    [Fact]
    public void RegisterScoped_ByType_NonGeneric()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterScoped(typeof(ConsoleGreeter));

        // Assert
        using var scope = container.CreateScope();
        var greeter1 = (ConsoleGreeter)scope.GetService(typeof(ConsoleGreeter));
        var greeter2 = (ConsoleGreeter)scope.GetService(typeof(ConsoleGreeter));

        Assert.Same(greeter1, greeter2);
    }

    [Fact]
    public void RegisterScoped_ServiceAndImplementation_NonGeneric()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterScoped(typeof(IGreeter), typeof(ConsoleGreeter));

        // Assert
        using var scope = container.CreateScope();
        var greeter1 = (IGreeter)scope.GetService(typeof(IGreeter));
        var greeter2 = (IGreeter)scope.GetService(typeof(IGreeter));

        Assert.Same(greeter1, greeter2);
        Assert.IsType<ConsoleGreeter>(greeter1);
    }

    [Fact]
    public void RegisterScoped_Generic_Single()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterScoped<ConsoleGreeter>();

        // Assert
        using var scope = container.CreateScope();
        var greeter1 = scope.GetService<ConsoleGreeter>();
        var greeter2 = scope.GetService<ConsoleGreeter>();

        Assert.Same(greeter1, greeter2);
    }

    [Fact]
    public void RegisterScoped_Generic_ServiceAndImplementation()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterScoped<IGreeter, ConsoleGreeter>();

        // Assert
        using var scope = container.CreateScope();
        var greeter1 = scope.GetService<IGreeter>();
        var greeter2 = scope.GetService<IGreeter>();

        Assert.Same(greeter1, greeter2);
        Assert.IsType<ConsoleGreeter>(greeter1);
    }

    [Fact]
    public void RegisterScoped_Generic_ServiceAndImplementationType()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.RegisterScoped<IGreeter>(typeof(ConsoleGreeter));

        // Assert
        using var scope = container.CreateScope();
        var greeter = scope.GetService<IGreeter>();

        Assert.NotNull(greeter);
        Assert.IsType<ConsoleGreeter>(greeter);
    }

    [Fact]
    public void RegisterScoped_DifferentScopesCreateDifferentInstances()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterScoped<IGreeter, ConsoleGreeter>();

        // Act & Assert
        using var scope1 = container.CreateScope();
        var greeter1 = scope1.GetService<IGreeter>();

        using var scope2 = container.CreateScope();
        var greeter2 = scope2.GetService<IGreeter>();

        Assert.NotSame(greeter1, greeter2);
    }

    [Fact]
    public void RegisterScoped_ByFactory_NonGeneric()
    {
        // Arrange
        var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.RegisterScoped(
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

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void RegisterScoped_ByFactory_Generic()
    {
        // Arrange
        var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.RegisterScoped<IGreeter>(scope =>
        {
            callCount++;
            return new ConsoleGreeter();
        });

        // Assert
        using var scope = container.CreateScope();
        scope.GetService<IGreeter>();
        scope.GetService<IGreeter>();

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void RegisterScoped_ByFactory_Generic_ServiceAndImplementation()
    {
        // Arrange
        var container = new SvcContainer();
        var callCount = 0;

        // Act
        container.RegisterScoped<IGreeter, ConsoleGreeter>(scope =>
        {
            callCount++;
            return new ConsoleGreeter();
        });

        // Assert
        using var scope = container.CreateScope();
        scope.GetService<IGreeter>();
        scope.GetService<IGreeter>();

        Assert.Equal(1, callCount);
    }
}
