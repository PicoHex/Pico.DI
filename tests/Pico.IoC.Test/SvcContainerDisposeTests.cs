namespace Pico.IoC.Test;

/// <summary>
/// Tests for Dispose and DisposeAsync functionality.
/// </summary>
public class SvcContainerDisposeTests : SvcContainerTestBase
{
    #region Disposable Test Services

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

    #region SvcContainer.Dispose Tests

    [Fact]
    public void Container_Dispose_DisposesRegisteredSingletonInstances()
    {
        // Arrange
        var disposableService = new DisposableService();
        var container = new SvcContainer();
        container.RegisterSingle<DisposableService>(disposableService);

        // Resolve to make sure it's registered
        using (var scope = container.CreateScope())
        {
            scope.GetService<DisposableService>();
        }

        // Act
        container.Dispose();

        // Assert
        Assert.True(disposableService.IsDisposed);
    }

    [Fact]
    public void Container_Dispose_CalledTwice_NoError()
    {
        // Arrange
        var container = new SvcContainer();

        // Act & Assert - Should not throw
        container.Dispose();
        container.Dispose();
    }

    [Fact]
    public void Container_AfterDispose_Register_ThrowsObjectDisposedException()
    {
        // Arrange
        var container = new SvcContainer();
        container.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(
            () => container.Register(new SvcDescriptor(typeof(IGreeter), _ => new ConsoleGreeter()))
        );
    }

    [Fact]
    public void Container_AfterDispose_CreateScope_ThrowsObjectDisposedException()
    {
        // Arrange
        var container = new SvcContainer();
        container.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => container.CreateScope());
    }

    #endregion

    #region SvcContainer.DisposeAsync Tests

    [Fact]
    public async Task Container_DisposeAsync_DisposesAsyncDisposableSingletonInstances()
    {
        // Arrange
        var asyncDisposableService = new AsyncDisposableService();
        var container = new SvcContainer();
        container.RegisterSingle<AsyncDisposableService>(asyncDisposableService);

        // Resolve to make sure it's registered
        await using (var scope = container.CreateScope())
        {
            scope.GetService<AsyncDisposableService>();
        }

        // Act
        await container.DisposeAsync();

        // Assert
        Assert.True(asyncDisposableService.IsDisposed);
    }

    [Fact]
    public async Task Container_DisposeAsync_DisposesIDisposableWhenNotAsyncDisposable()
    {
        // Arrange
        var disposableService = new DisposableService();
        var container = new SvcContainer();
        container.RegisterSingle<DisposableService>(disposableService);

        // Resolve to make sure it's registered
        await using (var scope = container.CreateScope())
        {
            scope.GetService<DisposableService>();
        }

        // Act
        await container.DisposeAsync();

        // Assert
        Assert.True(disposableService.IsDisposed);
    }

    [Fact]
    public async Task Container_DisposeAsync_PrefersAsyncDispose()
    {
        // Arrange
        var bothDisposable = new BothDisposableService();
        var container = new SvcContainer();
        container.RegisterSingle<BothDisposableService>(bothDisposable);

        // Resolve to make sure it's registered
        await using (var scope = container.CreateScope())
        {
            scope.GetService<BothDisposableService>();
        }

        // Act
        await container.DisposeAsync();

        // Assert - Should use async dispose, not sync dispose
        Assert.True(bothDisposable.IsAsyncDisposed);
        Assert.False(bothDisposable.IsDisposed);
    }

    [Fact]
    public async Task Container_DisposeAsync_CalledTwice_NoError()
    {
        // Arrange
        var container = new SvcContainer();

        // Act & Assert - Should not throw
        await container.DisposeAsync();
        await container.DisposeAsync();
    }

    #endregion

    #region SvcScope.Dispose Tests

    [Fact]
    public void Scope_Dispose_DisposesScopedInstances()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterScoped<DisposableService>(_ => new DisposableService());

        DisposableService? service;
        using (var scope = container.CreateScope())
        {
            service = scope.GetService<DisposableService>();
        }

        // Assert
        Assert.True(service.IsDisposed);
    }

    [Fact]
    public void Scope_Dispose_CalledTwice_NoError()
    {
        // Arrange
        var container = new SvcContainer();
        var scope = container.CreateScope();

        // Act & Assert - Should not throw
        scope.Dispose();
        scope.Dispose();
    }

    [Fact]
    public void Scope_AfterDispose_GetService_ThrowsObjectDisposedException()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterSingleton<IGreeter>(_ => new ConsoleGreeter());
        var scope = container.CreateScope();
        scope.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => scope.GetService<IGreeter>());
    }

    [Fact]
    public void Scope_AfterDispose_GetServices_ThrowsObjectDisposedException()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterSingleton<IGreeter>(_ => new ConsoleGreeter());
        var scope = container.CreateScope();
        scope.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => scope.GetServices<IGreeter>().ToList());
    }

    [Fact]
    public void Scope_AfterDispose_CreateScope_ThrowsObjectDisposedException()
    {
        // Arrange
        var container = new SvcContainer();
        var scope = container.CreateScope();
        scope.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => scope.CreateScope());
    }

    #endregion

    #region SvcScope.DisposeAsync Tests

    [Fact]
    public async Task Scope_DisposeAsync_DisposesAsyncDisposableScopedInstances()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterScoped<AsyncDisposableService>(_ => new AsyncDisposableService());

        AsyncDisposableService? service;
        await using (var scope = container.CreateScope())
        {
            service = scope.GetService<AsyncDisposableService>();
        }

        // Assert
        Assert.True(service.IsDisposed);
    }

    [Fact]
    public async Task Scope_DisposeAsync_DisposesIDisposableWhenNotAsyncDisposable()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterScoped<DisposableService>(_ => new DisposableService());

        DisposableService? service;
        await using (var scope = container.CreateScope())
        {
            service = scope.GetService<DisposableService>();
        }

        // Assert
        Assert.True(service.IsDisposed);
    }

    [Fact]
    public async Task Scope_DisposeAsync_PrefersAsyncDispose()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterScoped<BothDisposableService>(_ => new BothDisposableService());

        BothDisposableService? service;
        await using (var scope = container.CreateScope())
        {
            service = scope.GetService<BothDisposableService>();
        }

        // Assert - Should use async dispose, not sync dispose
        Assert.True(service.IsAsyncDisposed);
        Assert.False(service.IsDisposed);
    }

    [Fact]
    public async Task Scope_DisposeAsync_CalledTwice_NoError()
    {
        // Arrange
        var container = new SvcContainer();
        var scope = container.CreateScope();

        // Act & Assert - Should not throw
        await scope.DisposeAsync();
        await scope.DisposeAsync();
    }

    #endregion
}
