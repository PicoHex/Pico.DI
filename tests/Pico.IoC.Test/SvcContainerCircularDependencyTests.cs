namespace Pico.IoC.Test;

/// <summary>
/// Tests for circular dependency detection.
/// </summary>
public class SvcContainerCircularDependencyTests : SvcContainerTestBase
{
    #region Test Services with Circular Dependencies

    public interface IServiceA { }

    public interface IServiceB { }

    public interface IServiceC { }

    public class ServiceA(IServiceB serviceB) : IServiceA
    {
        public IServiceB ServiceB { get; } = serviceB;
    }

    public class ServiceB(IServiceA serviceA) : IServiceB
    {
        public IServiceA ServiceA { get; } = serviceA;
    }

    public class ServiceC(IServiceA serviceA) : IServiceC
    {
        public IServiceA ServiceA { get; } = serviceA;
    }

    // Three-way circular: A -> B -> C -> A
    public class ServiceA3(IServiceB serviceB) : IServiceA
    {
        public IServiceB ServiceB { get; } = serviceB;
    }

    public class ServiceB3(IServiceC serviceC) : IServiceB
    {
        public IServiceC ServiceC { get; } = serviceC;
    }

    public class ServiceC3(IServiceA serviceA) : IServiceC
    {
        public IServiceA ServiceA { get; } = serviceA;
    }

    // Self-referencing
    public interface ISelfRef { }

    public class SelfRefService(ISelfRef self) : ISelfRef
    {
        public ISelfRef Self { get; } = self;
    }

    // No circular dependency
    public class IndependentServiceA : IServiceA { }

    public class IndependentServiceB(IServiceA serviceA) : IServiceB
    {
        public IServiceA ServiceA { get; } = serviceA;
    }

    #endregion

    [Fact]
    public void CircularDependency_TwoServices_ThrowsException()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterTransient<IServiceA>(scope => new ServiceA(
            (IServiceB)scope.GetService(typeof(IServiceB))
        ));
        container.RegisterTransient<IServiceB>(scope => new ServiceB(
            (IServiceA)scope.GetService(typeof(IServiceA))
        ));

        using var scope = container.CreateScope();

        // Act & Assert
        var exception = Assert.Throws<PicoIocException>(() => scope.GetService<IServiceA>());
        Assert.Contains("Circular dependency detected", exception.Message);
    }

    [Fact]
    public void CircularDependency_ThreeServices_ThrowsException()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterTransient<IServiceA>(scope => new ServiceA3(
            (IServiceB)scope.GetService(typeof(IServiceB))
        ));
        container.RegisterTransient<IServiceB>(scope => new ServiceB3(
            (IServiceC)scope.GetService(typeof(IServiceC))
        ));
        container.RegisterTransient<IServiceC>(scope => new ServiceC3(
            (IServiceA)scope.GetService(typeof(IServiceA))
        ));

        using var scope = container.CreateScope();

        // Act & Assert
        var exception = Assert.Throws<PicoIocException>(() => scope.GetService<IServiceA>());
        Assert.Contains("Circular dependency detected", exception.Message);
    }

    [Fact]
    public void CircularDependency_SelfReference_ThrowsException()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterTransient<ISelfRef>(scope => new SelfRefService(
            (ISelfRef)scope.GetService(typeof(ISelfRef))
        ));

        using var scope = container.CreateScope();

        // Act & Assert
        var exception = Assert.Throws<PicoIocException>(() => scope.GetService<ISelfRef>());
        Assert.Contains("Circular dependency detected", exception.Message);
    }

    [Fact]
    public void NoCircularDependency_ResolvesSuccessfully()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterTransient<IServiceA>(_ => new IndependentServiceA());
        container.RegisterTransient<IServiceB>(scope => new IndependentServiceB(
            (IServiceA)scope.GetService(typeof(IServiceA))
        ));

        using var scope = container.CreateScope();

        // Act
        var serviceB = scope.GetService<IServiceB>();

        // Assert
        Assert.NotNull(serviceB);
        Assert.IsType<IndependentServiceB>(serviceB);
    }

    [Fact]
    public void CircularDependency_Singleton_ThrowsException()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterSingleton<IServiceA>(scope => new ServiceA(
            (IServiceB)scope.GetService(typeof(IServiceB))
        ));
        container.RegisterSingleton<IServiceB>(scope => new ServiceB(
            (IServiceA)scope.GetService(typeof(IServiceA))
        ));

        using var scope = container.CreateScope();

        // Act & Assert
        var exception = Assert.Throws<PicoIocException>(() => scope.GetService<IServiceA>());
        Assert.Contains("Circular dependency detected", exception.Message);
    }

    [Fact]
    public void CircularDependency_Scoped_ThrowsException()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterScoped<IServiceA>(scope => new ServiceA(
            (IServiceB)scope.GetService(typeof(IServiceB))
        ));
        container.RegisterScoped<IServiceB>(scope => new ServiceB(
            (IServiceA)scope.GetService(typeof(IServiceA))
        ));

        using var scope = container.CreateScope();

        // Act & Assert
        var exception = Assert.Throws<PicoIocException>(() => scope.GetService<IServiceA>());
        Assert.Contains("Circular dependency detected", exception.Message);
    }

    [Fact]
    public void CircularDependency_ExceptionMessage_ContainsDependencyChain()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterTransient<IServiceA>(scope => new ServiceA(
            (IServiceB)scope.GetService(typeof(IServiceB))
        ));
        container.RegisterTransient<IServiceB>(scope => new ServiceB(
            (IServiceA)scope.GetService(typeof(IServiceA))
        ));

        using var scope = container.CreateScope();

        // Act & Assert
        var exception = Assert.Throws<PicoIocException>(() => scope.GetService<IServiceA>());

        // The message should contain the dependency chain
        Assert.Contains("IServiceA", exception.Message);
        Assert.Contains("IServiceB", exception.Message);
        Assert.Contains("->", exception.Message);
    }

    [Fact]
    public void AfterCircularDependencyException_CanResolveOtherServices()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterTransient<IServiceA>(scope => new ServiceA(
            (IServiceB)scope.GetService(typeof(IServiceB))
        ));
        container.RegisterTransient<IServiceB>(scope => new ServiceB(
            (IServiceA)scope.GetService(typeof(IServiceA))
        ));
        container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());

        using var scope = container.CreateScope();

        // Act - First trigger circular dependency exception
        Assert.Throws<PicoIocException>(() => scope.GetService<IServiceA>());

        // Then try to resolve another service
        var greeter = scope.GetService<IGreeter>();

        // Assert - Should still be able to resolve other services
        Assert.NotNull(greeter);
        Assert.IsType<ConsoleGreeter>(greeter);
    }
}
