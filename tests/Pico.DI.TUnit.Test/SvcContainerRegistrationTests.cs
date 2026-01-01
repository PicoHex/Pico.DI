namespace Pico.DI.TUnit.Test;

/// <summary>
/// Tests for SvcContainer registration and basic operations.
/// </summary>
public class SvcContainerRegistrationTests : TUnitTestBase
{
    #region Register Method Tests

    [Test]
    public async Task Register_SvcDescriptor_AddsToContainer()
    {
        // Arrange
        using var container = new SvcContainer();
        var descriptor = new SvcDescriptor(
            typeof(IGreeter),
            _ => new ConsoleGreeter(),
            SvcLifetime.Transient
        );

        // Act
        container.Register(descriptor);
        using var scope = container.CreateScope();
        var service = scope.GetService<IGreeter>();

        // Assert
        await Assert.That(service).IsNotNull();
        await Assert.That(service).IsTypeOf<ConsoleGreeter>();
    }

    [Test]
    public async Task Register_MultipleDescriptors_AllAreResolvable()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act
        container.Register(
            new SvcDescriptor(typeof(IGreeter), _ => new ConsoleGreeter(), SvcLifetime.Transient)
        );
        container.Register(
            new SvcDescriptor(typeof(ILogger), _ => new ConsoleLogger(), SvcLifetime.Transient)
        );

        using var scope = container.CreateScope();

        // Assert
        var greeter = scope.GetService<IGreeter>();
        var logger = scope.GetService<ILogger>();

        await Assert.That(greeter).IsNotNull();
        await Assert.That(logger).IsNotNull();
    }

    [Test]
    public async Task Register_ReturnsContainerInstance_ForChaining()
    {
        // Arrange
        using var container = new SvcContainer();
        var descriptor = new SvcDescriptor(
            typeof(IGreeter),
            _ => new ConsoleGreeter(),
            SvcLifetime.Transient
        );

        // Act
        var result = container.Register(descriptor);

        // Assert
        await Assert.That(result).IsSameReferenceAs(container);
    }

    [Test]
    public async Task Register_AfterBuild_ThrowsInvalidOperationException()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        container.Build();

        var descriptor = new SvcDescriptor(
            typeof(ILogger),
            _ => new ConsoleLogger(),
            SvcLifetime.Transient
        );

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => container.Register(descriptor));
        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task Register_SameServiceType_OverridesWithLastRegistration()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        RegisterAlternativeGreeter(container);

        using var scope = container.CreateScope();

        // Act
        var greeter = scope.GetService<IGreeter>();

        // Assert - last registration wins
        await Assert.That(greeter).IsTypeOf<AlternativeGreeter>();
    }

    #endregion

    #region Build Method Tests

    [Test]
    public async Task Build_ReturnsContainerInstance()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);

        // Act
        var result = container.Build();

        // Assert
        await Assert.That(result).IsSameReferenceAs(container);
    }

    [Test]
    public async Task Build_CalledMultipleTimes_ReturnsWithoutError()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);

        // Act
        container.Build();
        var result = container.Build();

        // Assert
        await Assert.That(result).IsSameReferenceAs(container);
    }

    [Test]
    public async Task Build_OptimizesForPerformance_ServiceStillResolvable()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        container.Build();

        using var scope = container.CreateScope();

        // Act
        var greeter = scope.GetService<IGreeter>();

        // Assert
        await Assert.That(greeter).IsNotNull();
        await Assert.That(greeter).IsTypeOf<ConsoleGreeter>();
    }

    #endregion

    #region CreateScope Method Tests

    [Test]
    public async Task CreateScope_ReturnsNewScopeInstance()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);

        // Act
        using var scope = container.CreateScope();

        // Assert
        await Assert.That(scope).IsNotNull();
        await Assert.That(scope).IsTypeOf<SvcScope>();
    }

    [Test]
    public async Task CreateScope_MultipleCalls_ReturnDifferentInstances()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);

        // Act
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        // Assert
        await Assert.That(scope1).IsNotSameReferenceAs(scope2);
    }

    [Test]
    public async Task CreateScope_AfterBuild_UsesOptimizedCache()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        container.Build();

        // Act
        using var scope = container.CreateScope();
        var greeter = scope.GetService<IGreeter>();

        // Assert
        await Assert.That(greeter).IsNotNull();
    }

    #endregion
}
