namespace Pico.DI.Test;

/// <summary>
/// Tests for ISvcScope interface and extension methods.
/// </summary>
public class SvcScopeTests : XUnitTestBase
{
    #region GetService Tests

    [Fact]
    public void GetService_Generic_ReturnsTypedInstance()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IGreeter>();

        // Assert
        Assert.NotNull(service);
        Assert.IsType<ConsoleGreeter>(service);
    }

    [Fact]
    public void GetService_ByType_ReturnsObjectInstance()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService(typeof(IGreeter));

        // Assert
        Assert.NotNull(service);
        Assert.IsAssignableFrom<IGreeter>(service);
    }

    [Fact]
    public void GetService_LastRegistration_Wins()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());
        container.RegisterTransient<IGreeter>(_ => new AlternativeGreeter());

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IGreeter>();

        // Assert
        Assert.IsType<AlternativeGreeter>(service);
    }

    #endregion

    #region GetServices Tests

    [Fact]
    public void GetServices_Generic_ReturnsAllRegistrations()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());
        container.RegisterTransient<IGreeter>(_ => new AlternativeGreeter());

        using var scope = container.CreateScope();

        // Act
        var services = scope.GetServices<IGreeter>().ToList();

        // Assert
        Assert.Equal(2, services.Count);
        Assert.Single(services.OfType<ConsoleGreeter>());
        Assert.Single(services.OfType<AlternativeGreeter>());
    }

    [Fact]
    public void GetServices_ByType_ReturnsAllRegistrations()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());
        container.RegisterTransient<IGreeter>(_ => new AlternativeGreeter());

        using var scope = container.CreateScope();

        // Act
        var services = scope.GetServices(typeof(IGreeter)).ToList();

        // Assert
        Assert.Equal(2, services.Count);
    }

    [Fact]
    public void GetServices_MixedLifetimes_ResolvesCorrectly()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterSingleton<IGreeter>(_ => new ConsoleGreeter());
        container.RegisterTransient<IGreeter>(_ => new AlternativeGreeter());

        using var scope = container.CreateScope();

        // Act
        var services1 = scope.GetServices<IGreeter>().ToList();
        var services2 = scope.GetServices<IGreeter>().ToList();

        // Assert - singleton should be same, transient should be different
        Assert.Same(
            services1.First(s => s is ConsoleGreeter),
            services2.First(s => s is ConsoleGreeter)
        );
        Assert.NotSame(
            services1.First(s => s is AlternativeGreeter),
            services2.First(s => s is AlternativeGreeter)
        );
    }

    #endregion

    #region CreateScope Tests (Nested Scopes)

    [Fact]
    public void CreateScope_FromScope_CreatesNestedScope()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);

        using var parentScope = container.CreateScope();

        // Act
        using var childScope = parentScope.CreateScope();
        var service = childScope.GetService<IGreeter>();

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void NestedScope_Scoped_HasOwnInstances()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterScoped<CountingService>(_ => new CountingService());
        CountingService.ResetCounter();

        using var parentScope = container.CreateScope();
        using var childScope = parentScope.CreateScope();

        // Act
        var parentService = parentScope.GetService<CountingService>();
        var childService = childScope.GetService<CountingService>();

        // Assert
        Assert.NotSame(parentService, childService);
        Assert.NotEqual(parentService.InstanceId, childService.InstanceId);
    }

    [Fact]
    public void NestedScope_Singleton_SharesSameInstance()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterSingleton<CountingService>(_ => new CountingService());
        CountingService.ResetCounter();

        using var parentScope = container.CreateScope();
        using var childScope = parentScope.CreateScope();

        // Act
        var parentService = parentScope.GetService<CountingService>();
        var childService = childScope.GetService<CountingService>();

        // Assert
        Assert.Same(parentService, childService);
    }

    [Fact]
    public void NestedScope_MultipleLevel_ScopedAreIsolated()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterScoped<CountingService>(_ => new CountingService());
        CountingService.ResetCounter();

        using var level1 = container.CreateScope();
        using var level2 = level1.CreateScope();
        using var level3 = level2.CreateScope();

        // Act
        var service1 = level1.GetService<CountingService>();
        var service2 = level2.GetService<CountingService>();
        var service3 = level3.GetService<CountingService>();

        // Assert
        Assert.NotSame(service1, service2);
        Assert.NotSame(service2, service3);
        Assert.NotSame(service1, service3);
    }

    #endregion

    #region Built vs Non-Built Container Tests

    [Fact]
    public void Scope_FromBuiltContainer_ResolvesCorrectly()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        container.Build();

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IGreeter>();

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Scope_FromNonBuiltContainer_ResolvesCorrectly()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        // Note: Build() not called

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IGreeter>();

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Scope_FromBuiltContainer_NestedScope_ResolvesCorrectly()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        container.Build();

        using var scope1 = container.CreateScope();
        using var scope2 = scope1.CreateScope();

        // Act
        var service = scope2.GetService<IGreeter>();

        // Assert
        Assert.NotNull(service);
    }

    #endregion
}
