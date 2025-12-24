namespace Pico.IoC.Test;

/// <summary>
/// Tests for Register by Type with Lifetime methods.
/// </summary>
public class SvcContainerRegisterByTypeTests : SvcContainerTestBase
{
    [Fact]
    public void Register_ByType_WithSingleton_Lifetime()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.Register(typeof(ConsoleGreeter), SvcLifetime.Singleton);

        // Assert
        using var scope1 = container.CreateScope();
        var greeter1 = (ConsoleGreeter)scope1.GetService(typeof(ConsoleGreeter));

        using var scope2 = container.CreateScope();
        var greeter2 = (ConsoleGreeter)scope2.GetService(typeof(ConsoleGreeter));

        Assert.NotNull(greeter1);
        Assert.Same(greeter1, greeter2);
    }

    [Fact]
    public void Register_ByType_WithTransient_Lifetime()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.Register(typeof(ConsoleGreeter), SvcLifetime.Transient);

        // Assert
        using var scope = container.CreateScope();
        var greeter1 = (ConsoleGreeter)scope.GetService(typeof(ConsoleGreeter));
        var greeter2 = (ConsoleGreeter)scope.GetService(typeof(ConsoleGreeter));

        Assert.NotNull(greeter1);
        Assert.NotSame(greeter1, greeter2);
    }

    [Fact]
    public void Register_ServiceAndImplementation_WithLifetime()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.Register(typeof(IGreeter), typeof(ConsoleGreeter), SvcLifetime.Singleton);

        // Assert
        using var scope = container.CreateScope();
        var greeter = (IGreeter)scope.GetService(typeof(IGreeter));

        Assert.NotNull(greeter);
        Assert.IsType<ConsoleGreeter>(greeter);
    }

    [Fact]
    public void Register_SameTypeAsBoth_WithLifetime()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.Register(typeof(ConsoleGreeter), SvcLifetime.Singleton);

        // Assert
        using var scope = container.CreateScope();
        var greeter = (ConsoleGreeter)scope.GetService(typeof(ConsoleGreeter));

        Assert.NotNull(greeter);
    }

    [Fact]
    public void RegisterGeneric_Single_WithSingletonLifetime()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.Register<ConsoleGreeter>(SvcLifetime.Singleton);

        // Assert
        using var scope1 = container.CreateScope();
        var greeter1 = scope1.GetService<ConsoleGreeter>();

        using var scope2 = container.CreateScope();
        var greeter2 = scope2.GetService<ConsoleGreeter>();

        Assert.NotNull(greeter1);
        Assert.Same(greeter1, greeter2);
    }

    [Fact]
    public void RegisterGeneric_Single_WithTransientLifetime()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.Register<ConsoleGreeter>(SvcLifetime.Transient);

        // Assert
        using var scope = container.CreateScope();
        var greeter1 = scope.GetService<ConsoleGreeter>();
        var greeter2 = scope.GetService<ConsoleGreeter>();

        Assert.NotNull(greeter1);
        Assert.NotSame(greeter1, greeter2);
    }

    [Fact]
    public void RegisterGeneric_ServiceAndImplementation_WithLifetime()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.Register<IGreeter, ConsoleGreeter>(SvcLifetime.Singleton);

        // Assert
        using var scope = container.CreateScope();
        var greeter = scope.GetService<IGreeter>();

        Assert.NotNull(greeter);
        Assert.IsType<ConsoleGreeter>(greeter);
    }

    [Fact]
    public void RegisterGeneric_ServiceAndImplementationType_WithLifetime()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        container.Register<IGreeter>(typeof(ConsoleGreeter), SvcLifetime.Singleton);

        // Assert
        using var scope = container.CreateScope();
        var greeter = scope.GetService<IGreeter>();

        Assert.NotNull(greeter);
        Assert.IsType<ConsoleGreeter>(greeter);
    }
}
