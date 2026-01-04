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
        using var container = CreateContainer();
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
        using var container = CreateContainer();
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
        using var container = CreateContainer();
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
        using var container = CreateContainer();
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
        using var container = CreateContainer();
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
        using var container = CreateContainer();
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
        using var container = CreateContainer();
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
        using var container = CreateContainer();
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
        using var container = CreateContainer();
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
        using var container = CreateContainer();
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
        using var container = CreateContainer();
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
        using var container = CreateContainer();
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
        using var container = CreateContainer();
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

    #region Child Scope Auto-Disposal Tests

    [Fact]
    public void ParentScope_Dispose_DisposesChildScopes()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterScoped<DisposableService>(_ => new DisposableService());

        var parentScope = container.CreateScope();
        var childScope = parentScope.CreateScope();
        var grandchildScope = childScope.CreateScope();

        var parentService = parentScope.GetService<DisposableService>();
        var childService = childScope.GetService<DisposableService>();
        var grandchildService = grandchildScope.GetService<DisposableService>();

        // Act - dispose parent only
        parentScope.Dispose();

        // Assert - all services should be disposed (depth-first)
        Assert.True(grandchildService.IsDisposed);
        Assert.True(childService.IsDisposed);
        Assert.True(parentService.IsDisposed);
    }

    [Fact]
    public async Task ParentScope_DisposeAsync_DisposesChildScopes()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterScoped<AsyncDisposableService>(_ => new AsyncDisposableService());

        var parentScope = container.CreateScope();
        var childScope = parentScope.CreateScope();

        var parentService = parentScope.GetService<AsyncDisposableService>();
        var childService = childScope.GetService<AsyncDisposableService>();

        // Act - dispose parent only
        await parentScope.DisposeAsync();

        // Assert - both services should be disposed
        Assert.True(childService.IsDisposed);
        Assert.True(parentService.IsDisposed);
    }

    [Fact]
    public void ChildScope_DisposedByParent_CannotResolveServices()
    {
        // Arrange
        using var container = CreateContainer();
        RegisterConsoleGreeter(container);

        var parentScope = container.CreateScope();
        var childScope = parentScope.CreateScope();

        // Act - dispose parent
        parentScope.Dispose();

        // Assert - child scope should throw ObjectDisposedException
        Assert.Throws<ObjectDisposedException>(() => childScope.GetService<IGreeter>());
    }

    [Fact]
    public void ChildScope_DisposedIndependently_DoesNotAffectParent()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterScoped<DisposableService>(_ => new DisposableService());

        using var parentScope = container.CreateScope();
        var childScope = parentScope.CreateScope();

        var parentService = parentScope.GetService<DisposableService>();
        var childService = childScope.GetService<DisposableService>();

        // Act - dispose child only
        childScope.Dispose();

        // Assert - child is disposed, parent is not
        Assert.True(childService.IsDisposed);
        Assert.False(parentService.IsDisposed);

        // Parent should still work
        var anotherService = parentScope.GetService<DisposableService>();
        Assert.Same(parentService, anotherService);
    }

    [Fact]
    public void MultipleChildScopes_AllDisposedByParent()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterScoped<DisposableService>(_ => new DisposableService());

        var parentScope = container.CreateScope();
        var child1 = parentScope.CreateScope();
        var child2 = parentScope.CreateScope();
        var child3 = parentScope.CreateScope();

        var service1 = child1.GetService<DisposableService>();
        var service2 = child2.GetService<DisposableService>();
        var service3 = child3.GetService<DisposableService>();

        // Act
        parentScope.Dispose();

        // Assert - all child services should be disposed
        Assert.True(service1.IsDisposed);
        Assert.True(service2.IsDisposed);
        Assert.True(service3.IsDisposed);
    }

    #endregion
}
