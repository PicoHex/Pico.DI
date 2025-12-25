namespace Pico.DI.Test;

/// <summary>
/// Extended tests for SvcContainer to improve code coverage.
/// Covers disposal, async disposal, and edge cases.
/// </summary>
public class SvcContainerExtendedTests : SvcContainerTestBase
{
    #region Test Services

    public class DisposableService : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    public class AsyncDisposableService : IAsyncDisposable
    {
        public bool IsDisposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    public class BothDisposableService : IDisposable, IAsyncDisposable
    {
        public bool IsDisposed { get; private set; }
        public bool IsAsyncDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;

        public ValueTask DisposeAsync()
        {
            IsAsyncDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var container = new SvcContainer();

        // Act & Assert
        container.Dispose();
        container.Dispose(); // Should not throw
    }

    [Fact]
    public void Register_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var container = new SvcContainer();
        container.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(
            () => container.RegisterTransient<IGreeter>(scope => new ConsoleGreeter())
        );
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
    public void Dispose_DisposesSingletonInstances()
    {
        // Arrange
        var container = new SvcContainer();
        DisposableService? service = null;

        container.RegisterSingleton<DisposableService>(scope =>
        {
            service = new DisposableService();
            return service;
        });

        using (var scope = container.CreateScope())
        {
            scope.GetService<DisposableService>();
        }

        // Act
        container.Dispose();

        // Assert
        Assert.True(service!.IsDisposed);
    }

    [Fact]
    public void Dispose_DoesNotThrowForNonDisposableSingletons()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterSingleton<IGreeter>(scope => new ConsoleGreeter());

        using (var scope = container.CreateScope())
        {
            scope.GetService<IGreeter>();
        }

        // Act & Assert
        container.Dispose(); // Should not throw
    }

    #endregion

    #region DisposeAsync Tests

    [Fact]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var container = new SvcContainer();

        // Act & Assert
        await container.DisposeAsync();
        await container.DisposeAsync(); // Should not throw
    }

    [Fact]
    public async Task DisposeAsync_DisposesAsyncDisposableSingletons()
    {
        // Arrange
        var container = new SvcContainer();
        AsyncDisposableService? service = null;

        container.RegisterSingleton<AsyncDisposableService>(scope =>
        {
            service = new AsyncDisposableService();
            return service;
        });

        using (var scope = container.CreateScope())
        {
            scope.GetService<AsyncDisposableService>();
        }

        // Act
        await container.DisposeAsync();

        // Assert
        Assert.True(service!.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_PrefersAsyncDisposable_WhenBothImplemented()
    {
        // Arrange
        var container = new SvcContainer();
        BothDisposableService? service = null;

        container.RegisterSingleton<BothDisposableService>(scope =>
        {
            service = new BothDisposableService();
            return service;
        });

        using (var scope = container.CreateScope())
        {
            scope.GetService<BothDisposableService>();
        }

        // Act
        await container.DisposeAsync();

        // Assert
        Assert.True(service!.IsAsyncDisposed);
        Assert.False(service.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_FallsBackToDispose_WhenOnlyIDisposable()
    {
        // Arrange
        var container = new SvcContainer();
        DisposableService? service = null;

        container.RegisterSingleton<DisposableService>(scope =>
        {
            service = new DisposableService();
            return service;
        });

        using (var scope = container.CreateScope())
        {
            scope.GetService<DisposableService>();
        }

        // Act
        await container.DisposeAsync();

        // Assert
        Assert.True(service!.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_DoesNotThrowForNonDisposableSingletons()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterSingleton<IGreeter>(scope => new ConsoleGreeter());

        using (var scope = container.CreateScope())
        {
            scope.GetService<IGreeter>();
        }

        // Act & Assert
        await container.DisposeAsync(); // Should not throw
    }

    #endregion

    #region Pre-existing Instance Disposal Tests

    [Fact]
    public void Dispose_DisposesPreExistingInstances()
    {
        // Arrange
        var container = new SvcContainer();
        var service = new DisposableService();
        container.RegisterSingle<DisposableService>(service);

        // Act
        container.Dispose();

        // Assert
        Assert.True(service.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_DisposesPreExistingAsyncDisposableInstances()
    {
        // Arrange
        var container = new SvcContainer();
        var service = new AsyncDisposableService();
        container.RegisterSingle<AsyncDisposableService>(service);

        // Act
        await container.DisposeAsync();

        // Assert
        Assert.True(service.IsDisposed);
    }

    #endregion

    #region Multiple Singleton Registration Disposal Tests

    [Fact]
    public void Dispose_DisposesAllSingletonInstances()
    {
        // Arrange
        var container = new SvcContainer();
        DisposableService? service1 = null;
        DisposableService? service2 = null;

        container.RegisterSingleton<IGreeter>(scope =>
        {
            service1 = new DisposableService();
            return new ConsoleGreeter();
        });
        container.RegisterSingleton<ILogger>(scope =>
        {
            service2 = new DisposableService();
            return new ConsoleLogger();
        });

        using (var scope = container.CreateScope())
        {
            scope.GetService<IGreeter>();
            scope.GetService<ILogger>();
        }

        // Create instances in SingleInstance
        var container2 = new SvcContainer();
        var disposable1 = new DisposableService();
        var disposable2 = new DisposableService();
        container2.RegisterSingle<DisposableService>(disposable1);

        // Act
        container2.Dispose();

        // Assert
        Assert.True(disposable1.IsDisposed);
    }

    #endregion
}
