namespace Pico.DI.Test;

/// <summary>
/// Tests for service disposal functionality.
/// </summary>
public class SvcContainerDisposeTests : XUnitTestBase
{
    #region Container Disposal

    [Fact]
    public void Dispose_Container_DisposesRegisteredSingletons()
    {
        // Arrange
        DisposableService? service;
        using (var container = CreateContainer())
        {
            container.RegisterSingleton<DisposableService>(_ => new DisposableService());
            using var scope = container.CreateScope();
            service = scope.GetService<DisposableService>();

            // Assert - not disposed yet
            Assert.False(service.IsDisposed);
        }

        // Assert - disposed after container disposal
        Assert.True(service.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_Container_DisposesAsyncDisposables()
    {
        // Arrange
        AsyncDisposableService? service;
        await using (var container = CreateContainer())
        {
            container.RegisterSingleton<AsyncDisposableService>(_ => new AsyncDisposableService());
            await using var scope = container.CreateScope();
            service = scope.GetService<AsyncDisposableService>();

            // Assert - not disposed yet
            Assert.False(service.IsDisposed);
        }

        // Assert - disposed after container disposal
        Assert.True(service.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_Container_PrefersAsyncDisposable_OverDisposable()
    {
        // Arrange
        BothDisposableService? service;
        await using (var container = CreateContainer())
        {
            container.RegisterSingleton<BothDisposableService>(_ => new BothDisposableService());
            await using var scope = container.CreateScope();
            service = scope.GetService<BothDisposableService>();

            // Assert - not disposed yet
            Assert.False(service.IsSyncDisposed);
            Assert.False(service.IsAsyncDisposed);
        }

        // Assert - async disposed after container disposal (not sync)
        Assert.False(service.IsSyncDisposed);
        Assert.True(service.IsAsyncDisposed);
    }

    [Fact]
    public void Dispose_AfterDispose_DoesNotThrow()
    {
        // Arrange
        var container = CreateContainer();
        RegisterConsoleGreeter(container);

        // Act
        container.Dispose();
        container.Dispose(); // Second dispose should not throw

        // Assert - if we got here, no exception was thrown
    }

    [Fact]
    public async Task DisposeAsync_AfterDisposeAsync_DoesNotThrow()
    {
        // Arrange
        var container = CreateContainer();
        RegisterConsoleGreeter(container);

        // Act
        await container.DisposeAsync();
        await container.DisposeAsync(); // Second dispose should not throw

        // Assert - if we got here, no exception was thrown
    }

    [Fact]
    public void Dispose_PreventsFurtherRegistration()
    {
        // Arrange
        var container = CreateContainer();
        container.Dispose();

        // Act & Assert
        var ex = Assert.Throws<ObjectDisposedException>(() => RegisterConsoleGreeter(container));
        Assert.NotNull(ex);
    }

    [Fact]
    public void Dispose_PreventsScopeCreation()
    {
        // Arrange
        var container = CreateContainer();
        container.Dispose();

        // Act & Assert
        var ex = Assert.Throws<ObjectDisposedException>(() => container.CreateScope());
        Assert.NotNull(ex);
    }

    #endregion

    #region Scope Disposal

    [Fact]
    public void Dispose_Scope_DisposesScopedInstances()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterScoped<DisposableService>(_ => new DisposableService());

        DisposableService? service;
        using (var scope = container.CreateScope())
        {
            service = scope.GetService<DisposableService>();

            // Assert - not disposed yet
            Assert.False(service.IsDisposed);
        }

        // Assert - disposed after scope disposal
        Assert.True(service.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_Scope_DisposesAsyncDisposables()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterScoped<AsyncDisposableService>(_ => new AsyncDisposableService());

        AsyncDisposableService? service;
        await using (var scope = container.CreateScope())
        {
            service = scope.GetService<AsyncDisposableService>();

            // Assert - not disposed yet
            Assert.False(service.IsDisposed);
        }

        // Assert - disposed after scope disposal
        Assert.True(service.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_Scope_PrefersAsyncDisposable_OverDisposable()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterScoped<BothDisposableService>(_ => new BothDisposableService());

        BothDisposableService? service;
        await using (var scope = container.CreateScope())
        {
            service = scope.GetService<BothDisposableService>();

            // Assert - not disposed yet
            Assert.False(service.IsSyncDisposed);
            Assert.False(service.IsAsyncDisposed);
        }

        // Assert - async disposed (not sync)
        Assert.False(service.IsSyncDisposed);
        Assert.True(service.IsAsyncDisposed);
    }

    [Fact]
    public void Dispose_Scope_DoesNotDispose_Singletons()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterSingleton<DisposableService>(_ => new DisposableService());

        DisposableService? service;
        using (var scope = container.CreateScope())
        {
            service = scope.GetService<DisposableService>();
        }

        // Assert - singleton should NOT be disposed after scope disposal
        Assert.False(service.IsDisposed);
    }

    [Fact]
    public void Dispose_Scope_DoesNotDispose_Transients()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterTransient<DisposableService>(_ => new DisposableService());

        DisposableService? service;
        using (var scope = container.CreateScope())
        {
            service = scope.GetService<DisposableService>();
        }

        // Assert - transient should NOT be disposed by scope (not tracked)
        Assert.False(service.IsDisposed);
    }

    [Fact]
    public void Scope_DisposeMultipleTimes_DoesNotThrow()
    {
        // Arrange
        using var container = CreateContainer();
        RegisterConsoleGreeter(container);

        var scope = container.CreateScope();

        // Act
        scope.Dispose();
        scope.Dispose(); // Second dispose should not throw

        // Assert - if we got here, no exception was thrown
    }

    [Fact]
    public async Task Scope_DisposeAsyncMultipleTimes_DoesNotThrow()
    {
        // Arrange
        using var container = CreateContainer();
        RegisterConsoleGreeter(container);

        var scope = container.CreateScope();

        // Act
        await scope.DisposeAsync();
        await scope.DisposeAsync(); // Second dispose should not throw

        // Assert - if we got here, no exception was thrown
    }

    [Fact]
    public void Scope_AfterDispose_GetServiceThrowsObjectDisposedException()
    {
        // Arrange
        using var container = CreateContainer();
        RegisterConsoleGreeter(container);

        var scope = container.CreateScope();
        scope.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => scope.GetService<IGreeter>());
    }

    [Fact]
    public void Scope_AfterDispose_GetServicesThrowsObjectDisposedException()
    {
        // Arrange
        using var container = CreateContainer();
        RegisterConsoleGreeter(container);

        var scope = container.CreateScope();
        scope.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => scope.GetServices<IGreeter>());
    }

    [Fact]
    public void Scope_AfterDispose_CreateScopeThrowsObjectDisposedException()
    {
        // Arrange
        using var container = CreateContainer();
        RegisterConsoleGreeter(container);

        var scope = container.CreateScope();
        scope.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => scope.CreateScope());
    }

    #endregion

    #region Container Auto-Disposes Root Scopes Tests

    [Fact]
    public void Container_Dispose_DisposesAllRootScopes()
    {
        // Arrange
        var container = CreateContainer();
        container.RegisterScoped<DisposableService>(_ => new DisposableService());

        var scope1 = container.CreateScope();
        var scope2 = container.CreateScope();
        var scope3 = container.CreateScope();

        var service1 = scope1.GetService<DisposableService>();
        var service2 = scope2.GetService<DisposableService>();
        var service3 = scope3.GetService<DisposableService>();

        // Act - dispose container only
        container.Dispose();

        // Assert - all scoped services should be disposed
        Assert.True(service1.IsDisposed);
        Assert.True(service2.IsDisposed);
        Assert.True(service3.IsDisposed);
    }

    [Fact]
    public async Task Container_DisposeAsync_DisposesAllRootScopes()
    {
        // Arrange
        var container = CreateContainer();
        container.RegisterScoped<AsyncDisposableService>(_ => new AsyncDisposableService());

        var scope1 = container.CreateScope();
        var scope2 = container.CreateScope();

        var service1 = scope1.GetService<AsyncDisposableService>();
        var service2 = scope2.GetService<AsyncDisposableService>();

        // Act - dispose container only
        await container.DisposeAsync();

        // Assert - all scoped services should be disposed
        Assert.True(service1.IsDisposed);
        Assert.True(service2.IsDisposed);
    }

    [Fact]
    public void Container_Dispose_DisposesNestedScopes()
    {
        // Arrange
        var container = CreateContainer();
        container.RegisterScoped<DisposableService>(_ => new DisposableService());

        var rootScope = container.CreateScope();
        var childScope = rootScope.CreateScope();
        var grandchildScope = childScope.CreateScope();

        var rootService = rootScope.GetService<DisposableService>();
        var childService = childScope.GetService<DisposableService>();
        var grandchildService = grandchildScope.GetService<DisposableService>();

        // Act - dispose container only
        container.Dispose();

        // Assert - all services at all levels should be disposed
        Assert.True(grandchildService.IsDisposed);
        Assert.True(childService.IsDisposed);
        Assert.True(rootService.IsDisposed);
    }

    [Fact]
    public void Container_Dispose_ScopesCannotResolveAfterwards()
    {
        // Arrange
        var container = CreateContainer();
        RegisterConsoleGreeter(container);

        var scope = container.CreateScope();

        // Act
        container.Dispose();

        // Assert - scope should throw ObjectDisposedException
        Assert.Throws<ObjectDisposedException>(() => scope.GetService<IGreeter>());
    }

    [Fact]
    public void Container_Dispose_ScopeDisposedIndependently_NoDoubleDispose()
    {
        // Arrange
        var container = CreateContainer();
        container.RegisterScoped<DisposableService>(_ => new DisposableService());

        var scope = container.CreateScope();
        var service = scope.GetService<DisposableService>();

        // Act - dispose scope first, then container
        scope.Dispose();
        Assert.True(service.IsDisposed);

        // This should not throw (idempotent dispose)
        container.Dispose();

        // Assert - still disposed, no exception
        Assert.True(service.IsDisposed);
    }

    #endregion
}
