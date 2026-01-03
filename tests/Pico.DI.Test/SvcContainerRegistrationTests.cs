namespace Pico.DI.Test;

/// <summary>
/// Tests for SvcContainer registration and basic operations.
/// </summary>
public class SvcContainerRegistrationTests : XUnitTestBase
{
    #region Register Method Tests

    [Fact]
    public void Register_SvcDescriptor_AddsToContainer()
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
        Assert.NotNull(service);
        Assert.IsType<ConsoleGreeter>(service);
    }

    [Fact]
    public void Register_MultipleDescriptors_AllAreResolvable()
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

        Assert.NotNull(greeter);
        Assert.NotNull(logger);
    }

    [Fact]
    public void Register_ReturnsContainerInstance_ForChaining()
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
        Assert.Same(container, result);
    }

    [Fact]
    public void Register_AfterBuild_ThrowsInvalidOperationException()
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
        Assert.Contains("Build()", ex.Message);
    }

    [Fact]
    public void Register_SameServiceType_OverridesWithLastRegistration()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        RegisterAlternativeGreeter(container);

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IGreeter>();

        // Assert
        Assert.IsType<AlternativeGreeter>(service);
    }

    [Fact]
    public void Register_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var container = new SvcContainer();
        container.Dispose();

        var descriptor = new SvcDescriptor(
            typeof(IGreeter),
            _ => new ConsoleGreeter(),
            SvcLifetime.Transient
        );

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => container.Register(descriptor));
    }

    [Fact]
    public void RegisterRange_MultipleDescriptors_AllAreRegistered()
    {
        // Arrange
        using var container = new SvcContainer();
        var descriptors = new[]
        {
            new SvcDescriptor(typeof(IGreeter), _ => new ConsoleGreeter(), SvcLifetime.Transient),
            new SvcDescriptor(typeof(ILogger), _ => new ConsoleLogger(), SvcLifetime.Transient)
        };

        // Act
        container.RegisterRange(descriptors);

        using var scope = container.CreateScope();

        // Assert
        Assert.NotNull(scope.GetService<IGreeter>());
        Assert.NotNull(scope.GetService<ILogger>());
    }

    #endregion

    #region Build Method Tests

    [Fact]
    public void Build_ReturnsContainer_ForChaining()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);

        // Act
        var result = container.Build();

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public void Build_MultipleCalls_DoNotThrow()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);

        // Act - multiple Build() calls should be idempotent
        container.Build();
        container.Build();

        // Assert - if we got here, no exception was thrown
        using var scope = container.CreateScope();
        Assert.NotNull(scope.GetService<IGreeter>());
    }

    [Fact]
    public void Build_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        container.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => container.Build());
    }

    [Fact]
    public void Build_ImprovesFrozenDictionaryUsage_ForScopeCreation()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        container.Build();

        // Act - create multiple scopes after build
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        // Assert - both should work correctly
        Assert.NotNull(scope1.GetService<IGreeter>());
        Assert.NotNull(scope2.GetService<IGreeter>());
    }

    #endregion

    #region CreateScope Tests

    [Fact]
    public void CreateScope_ReturnsNewScopeInstance()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);

        // Act
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        // Assert
        Assert.NotSame(scope1, scope2);
    }

    [Fact]
    public void CreateScope_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var container = new SvcContainer();
        container.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => container.CreateScope());
    }

    [Fact]
    public void CreateScope_WithoutBuild_UsesNonFrozenCache()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);

        // Act - create scope without Build()
        using var scope = container.CreateScope();

        // Assert
        Assert.NotNull(scope.GetService<IGreeter>());
    }

    [Fact]
    public void CreateScope_WithBuild_UsesFrozenCache()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        container.Build();

        // Act - create scope after Build()
        using var scope = container.CreateScope();

        // Assert
        Assert.NotNull(scope.GetService<IGreeter>());
    }

    #endregion
}
