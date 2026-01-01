namespace Pico.DI.TUnit.Test;

/// <summary>
/// Tests for service disposal functionality.
/// </summary>
public class SvcContainerDisposeTests : TUnitTestBase
{
    #region Container Disposal

    [Test]
    public async Task Dispose_Container_DisposesRegisteredSingletons()
    {
        // Arrange
        DisposableService? service;
        using (var container = new SvcContainer())
        {
            container.RegisterSingleton<DisposableService>(_ => new DisposableService());
            using var scope = container.CreateScope();
            service = scope.GetService<DisposableService>();

            // Assert - not disposed yet
            await Assert.That(service.IsDisposed).IsFalse();
        }

        // Assert - disposed after container disposal
        await Assert.That(service.IsDisposed).IsTrue();
    }

    [Test]
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
            await Assert.That(service.IsDisposed).IsFalse();
        }

        // Assert - disposed after container disposal
        await Assert.That(service.IsDisposed).IsTrue();
    }

    [Test]
    public async Task Dispose_AfterDispose_DoesNotThrow()
    {
        // Arrange
        var container = new SvcContainer();
        RegisterConsoleGreeter(container);

        // Act
        container.Dispose();
        container.Dispose(); // Second dispose should not throw

        // Assert - if we got here, no exception was thrown
        await Task.CompletedTask;
    }

    [Test]
    public async Task Dispose_PreventsFurtherRegistration()
    {
        // Arrange
        var container = new SvcContainer();
        container.Dispose();

        // Act & Assert
        var ex = Assert.Throws<ObjectDisposedException>(() => RegisterConsoleGreeter(container));
        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task Dispose_PreventsScopeCreation()
    {
        // Arrange
        var container = new SvcContainer();
        container.Dispose();

        // Act & Assert
        var ex = Assert.Throws<ObjectDisposedException>(() => container.CreateScope());
        await Assert.That(ex).IsNotNull();
    }

    #endregion

    #region Scope Disposal

    [Test]
    public async Task Dispose_Scope_DisposesScopedInstances()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterScoped<DisposableService>(_ => new DisposableService());

        DisposableService? service;
        using (var scope = container.CreateScope())
        {
            service = scope.GetService<DisposableService>();
            await Assert.That(service.IsDisposed).IsFalse();
        }

        // Assert
        await Assert.That(service.IsDisposed).IsTrue();
    }

    [Test]
    public async Task DisposeAsync_Scope_DisposesAsyncScopedInstances()
    {
        // Arrange
        await using var container = new SvcContainer();
        container.RegisterScoped<AsyncDisposableService>(_ => new AsyncDisposableService());

        AsyncDisposableService? service;
        await using (var scope = container.CreateScope())
        {
            service = scope.GetService<AsyncDisposableService>();
            await Assert.That(service.IsDisposed).IsFalse();
        }

        // Assert
        await Assert.That(service.IsDisposed).IsTrue();
    }

    [Test]
    public async Task Dispose_Scope_DoesNotDisposeSingletons()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterSingleton<DisposableService>(_ => new DisposableService());

        DisposableService? service;
        using (var scope = container.CreateScope())
        {
            service = scope.GetService<DisposableService>();
        }

        // Assert - singleton should still be available
        await Assert.That(service.IsDisposed).IsFalse();

        // Clean up - disposing container will dispose singleton
        container.Dispose();
        await Assert.That(service.IsDisposed).IsTrue();
    }

    [Test]
    public async Task Dispose_Scope_PreventsServiceResolution()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        var scope = container.CreateScope();

        // Act
        scope.Dispose();

        // Assert
        var ex = Assert.Throws<ObjectDisposedException>(() => scope.GetService<IGreeter>());
        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task Dispose_MultipleScopedInstances_AllDisposed()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterScoped<DisposableService>(_ => new DisposableService());

        var services = new List<DisposableService>();

        using (var scope = container.CreateScope())
        {
            // Register multiple instances with different types
            container.RegisterScoped<IGreeter>(_ => new ConsoleGreeter());

            services.Add(scope.GetService<DisposableService>());
        }

        // Assert
        foreach (var service in services)
        {
            await Assert.That(service.IsDisposed).IsTrue();
        }
    }

    #endregion

    #region Both IDisposable and IAsyncDisposable

    [Test]
    public async Task Dispose_BothDisposable_UsesSyncDispose()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterScoped<BothDisposableService>(_ => new BothDisposableService());

        BothDisposableService? service;
        using (var scope = container.CreateScope())
        {
            service = scope.GetService<BothDisposableService>();
        }

        // Assert - sync dispose should be called
        await Assert.That(service.IsSyncDisposed).IsTrue();
    }

    [Test]
    public async Task DisposeAsync_BothDisposable_UsesAsyncDispose()
    {
        // Arrange
        await using var container = new SvcContainer();
        container.RegisterScoped<BothDisposableService>(_ => new BothDisposableService());

        BothDisposableService? service;
        await using (var scope = container.CreateScope())
        {
            service = scope.GetService<BothDisposableService>();
        }

        // Assert - async dispose should be called
        await Assert.That(service.IsAsyncDisposed).IsTrue();
    }

    #endregion
}
