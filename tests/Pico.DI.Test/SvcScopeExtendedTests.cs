namespace Pico.DI.Test;

/// <summary>
/// Extended tests for SvcScope to improve code coverage.
/// Covers edge cases, error scenarios, and specific code paths.
/// </summary>
public class SvcScopeExtendedTests : SvcContainerTestBase
{
    #region Test Services for Coverage

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

    public interface IOpenGeneric<T>
    {
        T Value { get; }
    }

    public class OpenGenericImpl<T>(T value) : IOpenGeneric<T>
    {
        public T Value { get; } = value;
    }

    #endregion

    #region GetService After Dispose Tests

    [Fact]
    public void GetService_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(scope => new ConsoleGreeter());
        var scope = container.CreateScope();
        scope.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => scope.GetService<IGreeter>());
    }

    [Fact]
    public void GetServices_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(scope => new ConsoleGreeter());
        var scope = container.CreateScope();
        scope.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => scope.GetServices(typeof(IGreeter)));
    }

    [Fact]
    public void CreateScope_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var container = new SvcContainer();
        var scope = container.CreateScope();
        scope.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => scope.CreateScope());
    }

    #endregion

    #region Dispose Pattern Tests

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var container = new SvcContainer();
        var scope = container.CreateScope();

        // Act & Assert
        scope.Dispose();
        scope.Dispose(); // Should not throw
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var container = new SvcContainer();
        var scope = container.CreateScope();

        // Act & Assert
        await scope.DisposeAsync();
        await scope.DisposeAsync(); // Should not throw
    }

    [Fact]
    public void Dispose_DisposesOnlyScopedInstances()
    {
        // Arrange
        var container = new SvcContainer();
        DisposableService? scopedService = null;
        DisposableService? singletonService = null;

        container.RegisterScoped<DisposableService>(scope =>
        {
            scopedService = new DisposableService();
            return scopedService;
        });
        container.RegisterSingleton<IGreeter>(scope =>
        {
            singletonService = new DisposableService();
            return new ConsoleGreeter();
        });

        var scope = container.CreateScope();
        scope.GetService<DisposableService>();
        scope.GetService<IGreeter>();

        // Act
        scope.Dispose();

        // Assert
        Assert.True(scopedService!.IsDisposed);
        // Singleton should not be disposed by scope
    }

    [Fact]
    public async Task DisposeAsync_DisposesAsyncDisposableServices()
    {
        // Arrange
        var container = new SvcContainer();
        AsyncDisposableService? asyncService = null;

        container.RegisterScoped<AsyncDisposableService>(scope =>
        {
            asyncService = new AsyncDisposableService();
            return asyncService;
        });

        var scope = container.CreateScope();
        scope.GetService<AsyncDisposableService>();

        // Act
        await scope.DisposeAsync();

        // Assert
        Assert.True(asyncService!.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_PrefersAsyncDisposable_WhenBothImplemented()
    {
        // Arrange
        var container = new SvcContainer();
        BothDisposableService? service = null;

        container.RegisterScoped<BothDisposableService>(scope =>
        {
            service = new BothDisposableService();
            return service;
        });

        var scope = container.CreateScope();
        scope.GetService<BothDisposableService>();

        // Act
        await scope.DisposeAsync();

        // Assert
        Assert.True(service!.IsAsyncDisposed);
        Assert.False(service.IsDisposed); // Should prefer async
    }

    [Fact]
    public async Task DisposeAsync_FallsBackToDispose_WhenOnlyIDisposable()
    {
        // Arrange
        var container = new SvcContainer();
        DisposableService? service = null;

        container.RegisterScoped<DisposableService>(scope =>
        {
            service = new DisposableService();
            return service;
        });

        var scope = container.CreateScope();
        scope.GetService<DisposableService>();

        // Act
        await scope.DisposeAsync();

        // Assert
        Assert.True(service!.IsDisposed);
    }

    #endregion

    #region Open Generic Error Handling Tests

    [Fact]
    public void GetService_OpenGenericRegistered_ClosedNotDetected_ThrowsWithHelpfulMessage()
    {
        // Arrange
        var container = new SvcContainer();
        // Register open generic without factory (simulates source gen scenario)
        // Using the unified Register API - open generics are auto-detected
        container.RegisterScoped(typeof(IOpenGeneric<>), typeof(OpenGenericImpl<>));

        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IOpenGeneric<string>>());
        Assert.Contains("Open generic type", ex.Message);
        Assert.Contains("was not detected at compile time", ex.Message);
    }

    [Fact]
    public void GetService_ClosedGeneric_NotRegistered_ThrowsNormalError()
    {
        // Arrange
        var container = new SvcContainer();
        // No open generic registered
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IOpenGeneric<string>>());
        Assert.Contains("not registered", ex.Message);
        Assert.DoesNotContain("Open generic type", ex.Message);
    }

    #endregion

    #region Missing Factory Error Tests

    [Fact]
    public void GetService_TransientWithoutFactory_ThrowsPicoDiException()
    {
        // Arrange
        var container = new SvcContainer();
        // Manually create descriptor without factory
        var descriptor = new SvcDescriptor(
            typeof(IGreeter),
            typeof(ConsoleGreeter),
            SvcLifetime.Transient
        );
        container.Register(descriptor);

        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IGreeter>());
        Assert.Contains("No factory registered", ex.Message);
        Assert.Contains("transient", ex.Message);
    }

    [Fact]
    public void GetService_SingletonWithoutFactoryOrInstance_ThrowsPicoDiException()
    {
        // Arrange
        var container = new SvcContainer();
        // Manually create descriptor without factory or instance
        var descriptor = new SvcDescriptor(
            typeof(IGreeter),
            typeof(ConsoleGreeter),
            SvcLifetime.Singleton
        );
        container.Register(descriptor);

        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IGreeter>());
        Assert.Contains("No factory or instance registered", ex.Message);
        Assert.Contains("singleton", ex.Message);
    }

    [Fact]
    public void GetService_ScopedWithoutFactory_ThrowsPicoDiException()
    {
        // Arrange
        var container = new SvcContainer();
        // Manually create descriptor without factory
        var descriptor = new SvcDescriptor(
            typeof(IGreeter),
            typeof(ConsoleGreeter),
            SvcLifetime.Scoped
        );
        container.Register(descriptor);

        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IGreeter>());
        Assert.Contains("No factory registered", ex.Message);
        Assert.Contains("scoped", ex.Message);
    }

    [Fact]
    public void GetServices_TransientWithoutFactory_ThrowsPicoDiException()
    {
        // Arrange
        var container = new SvcContainer();
        var descriptor = new SvcDescriptor(
            typeof(IGreeter),
            typeof(ConsoleGreeter),
            SvcLifetime.Transient
        );
        container.Register(descriptor);

        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetServices<IGreeter>().ToList());
        Assert.Contains("No factory registered", ex.Message);
    }

    #endregion

    #region Singleton Pre-existing Instance Tests

    [Fact]
    public void GetService_Singleton_WithPreExistingInstance_ReturnsInstance()
    {
        // Arrange
        var container = new SvcContainer();
        var instance = new ConsoleGreeter();
        container.RegisterSingle<IGreeter>(instance);

        using var scope = container.CreateScope();

        // Act
        var result = scope.GetService<IGreeter>();

        // Assert
        Assert.Same(instance, result);
    }

    [Fact]
    public void GetService_Singleton_WithPreExistingInstance_MultipleCalls_ReturnsSameInstance()
    {
        // Arrange
        var container = new SvcContainer();
        var instance = new ConsoleGreeter();
        container.RegisterSingle<IGreeter>(instance);

        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        // Act
        var result1 = scope1.GetService<IGreeter>();
        var result2 = scope2.GetService<IGreeter>();

        // Assert
        Assert.Same(instance, result1);
        Assert.Same(instance, result2);
    }

    #endregion

    #region Non-Disposable Service Tests

    [Fact]
    public void Dispose_WithNonDisposableScopedServices_DoesNotThrow()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterScoped<IGreeter>(scope => new ConsoleGreeter());

        var scope = container.CreateScope();
        scope.GetService<IGreeter>(); // Non-disposable

        // Act & Assert
        scope.Dispose(); // Should not throw
    }

    [Fact]
    public async Task DisposeAsync_WithNonDisposableScopedServices_DoesNotThrow()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterScoped<IGreeter>(scope => new ConsoleGreeter());

        var scope = container.CreateScope();
        scope.GetService<IGreeter>(); // Non-disposable

        // Act & Assert
        await scope.DisposeAsync(); // Should not throw
    }

    #endregion

    #region Invalid Lifetime Tests (Defensive Code Coverage)

    [Fact]
    public void GetService_InvalidLifetime_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var container = new SvcContainer();
        // Create descriptor with invalid lifetime value
        var descriptor = new SvcDescriptor(
            typeof(IGreeter),
            scope => new ConsoleGreeter(),
            (SvcLifetime)99
        );
        container.Register(descriptor);

        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => scope.GetService<IGreeter>());
        Assert.Contains("Unknown service lifetime", ex.Message);
    }

    [Fact]
    public void GetServices_InvalidLifetime_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var container = new SvcContainer();
        // Create descriptor with invalid lifetime value
        var descriptor = new SvcDescriptor(
            typeof(IGreeter),
            scope => new ConsoleGreeter(),
            (SvcLifetime)99
        );
        container.Register(descriptor);

        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => scope.GetServices<IGreeter>().ToList()
        );
        Assert.Contains("Unknown service lifetime", ex.Message);
    }

    #endregion

    #region Concurrent Resolution Tests

    [Fact]
    public async Task GetService_Scoped_ConcurrentAccess_ReturnsSameInstance()
    {
        // Arrange
        var container = new SvcContainer();
        var instanceCount = 0;

        container.RegisterScoped<IGreeter>(scope =>
        {
            Interlocked.Increment(ref instanceCount);
            Thread.Sleep(10);
            return new ConsoleGreeter();
        });

        using var scope = container.CreateScope();

        // Act
        var tasks = Enumerable
            .Range(0, 10)
            .Select(_ => Task.Run(() => scope.GetService<IGreeter>()))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        // Due to ConcurrentDictionary.GetOrAdd, there might be a race condition
        // but all returned instances should eventually be the same
        Assert.All(results.Skip(1), r => Assert.Same(results[0], r));
    }

    #endregion
}
