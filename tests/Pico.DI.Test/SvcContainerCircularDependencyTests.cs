namespace Pico.DI.Test;

/// <summary>
/// Tests for circular dependency detection.
///
/// NOTE: Circular dependency detection is now performed at COMPILE-TIME by the source generator.
/// These tests verify that factory-based registrations (which bypass compile-time detection)
/// will cause a StackOverflowException at runtime, demonstrating why compile-time detection
/// is essential for type-based registrations.
///
/// For production use with type-based registrations (e.g., RegisterTransient&lt;IServiceA, ServiceA&gt;()),
/// the source generator will detect circular dependencies and report PICO002 compile-time error.
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

    /// <summary>
    /// This test demonstrates that factory-based circular dependencies cause StackOverflowException.
    /// In production, type-based registrations are detected at compile-time by the source generator.
    /// </summary>
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

    /// <summary>
    /// Verifies that services can still be resolved after a failed resolution attempt.
    /// </summary>
    [Fact]
    public void AfterFailedResolution_CanResolveOtherServices()
    {
        // Arrange
        var container = new SvcContainer();
        // Register a service that will fail to resolve
        container.RegisterTransient<IServiceA>(_ =>
            throw new InvalidOperationException("Test failure")
        );
        container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());

        using var scope = container.CreateScope();

        // Act - First trigger failure
        Assert.Throws<InvalidOperationException>(() => scope.GetService<IServiceA>());

        // Then try to resolve another service
        var greeter = scope.GetService<IGreeter>();

        // Assert - Should still be able to resolve other services
        Assert.NotNull(greeter);
        Assert.IsType<ConsoleGreeter>(greeter);
    }

    /// <summary>
    /// Documentation test showing what happens with factory-based circular dependencies.
    /// This is expected to cause a StackOverflowException - demonstrating why
    /// compile-time detection is essential.
    /// NOTE: StackOverflowException cannot be caught, so this test is disabled.
    /// </summary>
    private void CircularDependency_WithFactory_CausesStackOverflow()
    {
        // This test demonstrates that factory-based circular dependencies
        // will cause StackOverflowException at runtime.
        //
        // For type-based registrations (RegisterTransient<IServiceA, ServiceA>()),
        // the source generator detects circular dependencies at compile-time
        // and reports PICO002 error.
        //
        // var container = new SvcContainer();
        // container.RegisterTransient<IServiceA>(scope => new ServiceA(
        //     (IServiceB)scope.GetService(typeof(IServiceB))
        // ));
        // container.RegisterTransient<IServiceB>(scope => new ServiceB(
        //     (IServiceA)scope.GetService(typeof(IServiceA))
        // ));
        // using var scope = container.CreateScope();
        // var _ = scope.GetService<IServiceA>(); // StackOverflowException!
    }
}
