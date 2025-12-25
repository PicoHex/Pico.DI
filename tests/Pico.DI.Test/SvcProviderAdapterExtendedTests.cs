namespace Pico.DI.Test;

/// <summary>
/// Extended tests for SvcProviderAdapter to improve code coverage.
/// Covers dispose patterns, edge cases, and adapter-specific functionality.
/// </summary>
public class SvcProviderAdapterExtendedTests : SvcContainerTestBase
{
    #region Constructor Tests

    [Fact]
    public void SvcProviderAdapter_NullScope_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SvcProviderAdapter(null!));
    }

    #endregion

    #region GetService After Dispose Tests

    [Fact]
    public void GetService_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(scope => new ConsoleGreeter());
        var adapter = container.CreateServiceProviderScope();
        adapter.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(
            () => ((IServiceProvider)adapter).GetService(typeof(IGreeter))
        );
    }

    [Fact]
    public void ISvcScope_GetService_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(scope => new ConsoleGreeter());
        var adapter = container.CreateServiceProviderScope();
        adapter.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(
            () => ((ISvcScope)adapter).GetService(typeof(IGreeter))
        );
    }

    [Fact]
    public void GetServices_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(scope => new ConsoleGreeter());
        var adapter = container.CreateServiceProviderScope();
        adapter.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => adapter.GetServices(typeof(IGreeter)));
    }

    [Fact]
    public void CreateScope_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var container = new SvcContainer();
        var adapter = container.CreateServiceProviderScope();
        adapter.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => adapter.CreateScope());
    }

    #endregion

    #region Double Dispose Tests

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var container = new SvcContainer();
        var adapter = container.CreateServiceProviderScope();

        // Act & Assert - Should not throw
        adapter.Dispose();
        adapter.Dispose();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var container = new SvcContainer();
        var adapter = container.CreateServiceProviderScope();

        // Act & Assert - Should not throw
        await adapter.DisposeAsync();
        await adapter.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_AfterDispose_DoesNotThrow()
    {
        // Arrange
        var container = new SvcContainer();
        var adapter = container.CreateServiceProviderScope();

        // Act & Assert - Mixed disposal should not throw
        adapter.Dispose();
        await adapter.DisposeAsync();
    }

    #endregion

    #region GetServices Tests

    [Fact]
    public void ISvcScope_GetService_ReturnsServiceWhenRegistered()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(scope => new ConsoleGreeter());
        using var adapter = container.CreateServiceProviderScope();

        // Act - Explicitly call ISvcScope.GetService
        var service = ((ISvcScope)adapter).GetService(typeof(IGreeter));

        // Assert
        Assert.NotNull(service);
        Assert.IsType<ConsoleGreeter>(service);
    }

    [Fact]
    public void GetServices_ReturnsAllRegisteredServices()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(scope => new ConsoleGreeter());
        container.RegisterTransient<IGreeter>(scope => new AlternativeGreeter());
        using var adapter = container.CreateServiceProviderScope();

        // Act
        var services = adapter.GetServices(typeof(IGreeter)).ToList();

        // Assert
        Assert.Equal(2, services.Count);
        Assert.Contains(services, s => s is ConsoleGreeter);
        Assert.Contains(services, s => s is AlternativeGreeter);
    }

    #endregion

    #region AsServiceProvider Extension Tests

    [Fact]
    public void AsServiceProvider_WhenAlreadyAdapter_ReturnsSameInstance()
    {
        // Arrange
        var container = new SvcContainer();
        var adapter = container.CreateServiceProviderScope();

        // Act
        var result = adapter.AsServiceProvider();

        // Assert
        Assert.Same(adapter, result);
    }

    [Fact]
    public void AsServiceProvider_WhenNotAdapter_CreatesNewAdapter()
    {
        // Arrange
        var container = new SvcContainer();
        using var scope = container.CreateScope();

        // Act
        var adapter = scope.AsServiceProvider();

        // Assert
        Assert.IsType<SvcProviderAdapter>(adapter);
        Assert.NotSame(scope, adapter);
    }

    #endregion

    #region GetServiceOrDefault Non-Generic Tests

    [Fact]
    public void GetServiceOrDefault_NonGeneric_ReturnsNullForUnregistered()
    {
        // Arrange
        var container = new SvcContainer();
        using var scope = container.CreateScope();

        // Act
        var service = scope.GetServiceOrDefault(typeof(IGreeter));

        // Assert
        Assert.Null(service);
    }

    [Fact]
    public void GetServiceOrDefault_NonGeneric_ReturnsServiceWhenRegistered()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(scope => new ConsoleGreeter());
        using var scope = container.CreateScope();

        // Act
        var service = scope.GetServiceOrDefault(typeof(IGreeter));

        // Assert
        Assert.NotNull(service);
        Assert.IsType<ConsoleGreeter>(service);
    }

    #endregion

    #region GetRequiredService Non-Generic Tests

    [Fact]
    public void GetRequiredService_NonGeneric_ThrowsForUnregistered()
    {
        // Arrange
        var container = new SvcContainer();
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => scope.GetRequiredService(typeof(IGreeter))
        );
        Assert.Contains("has been registered", ex.Message);
        Assert.NotNull(ex.InnerException);
        Assert.IsType<PicoDiException>(ex.InnerException);
    }

    [Fact]
    public void GetRequiredService_NonGeneric_ReturnsServiceWhenRegistered()
    {
        // Arrange
        var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(scope => new ConsoleGreeter());
        using var scope = container.CreateScope();

        // Act
        var service = scope.GetRequiredService(typeof(IGreeter));

        // Assert
        Assert.NotNull(service);
        Assert.IsType<ConsoleGreeter>(service);
    }

    #endregion
}
