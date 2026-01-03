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
        using (var container = new SvcContainer())
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
        await using (var container = new SvcContainer())
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
        await using (var container = new SvcContainer())
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
        var container = new SvcContainer();
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
        var container = new SvcContainer();
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
        var container = new SvcContainer();
        container.Dispose();

        // Act & Assert
        var ex = Assert.Throws<ObjectDisposedException>(() => RegisterConsoleGreeter(container));
        Assert.NotNull(ex);
    }

    [Fact]
    public void Dispose_PreventsScopeCreation()
    {
        // Arrange
        var container = new SvcContainer();
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
        using var container = new SvcContainer();
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
        using var container = new SvcContainer();
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
        using var container = new SvcContainer();
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
        using var container = new SvcContainer();
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
        using var container = new SvcContainer();
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
        using var container = new SvcContainer();
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
        using var container = new SvcContainer();
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
        using var container = new SvcContainer();
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
        using var container = new SvcContainer();
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
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);

        var scope = container.CreateScope();
        scope.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => scope.CreateScope());
    }

    #endregion
}
