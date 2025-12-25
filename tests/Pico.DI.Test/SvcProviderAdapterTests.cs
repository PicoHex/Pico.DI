namespace Pico.DI.Test;

/// <summary>
/// Tests for IServiceProvider adapter functionality.
/// </summary>
public class SvcProviderAdapterTests : SvcContainerTestBase
{
    #region Test Services

    public class ServiceWithMultipleDeps(IGreeter greeter, ILogger logger)
    {
        public IGreeter Greeter { get; } = greeter;
        public ILogger Logger { get; } = logger;
    }

    #endregion

    [Fact]
    public void AsServiceProvider_ReturnsAdapter()
    {
        // Arrange
        using var container = new SvcContainer();
        using var scope = container.CreateScope();

        // Act
        var adapter = scope.AsServiceProvider();

        // Assert
        Assert.IsType<SvcProviderAdapter>(adapter);
        Assert.IsAssignableFrom<IServiceProvider>(adapter);
    }

    [Fact]
    public void CreateServiceProviderScope_ReturnsAdapter()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(scope => new ConsoleGreeter());

        // Act
        using var adapter = container.CreateServiceProviderScope();

        // Assert
        Assert.IsType<SvcProviderAdapter>(adapter);
    }

    [Fact]
    public void IServiceProvider_GetService_ReturnsNullForUnregistered()
    {
        // Arrange
        using var container = new SvcContainer();
        using var adapter = container.CreateServiceProviderScope();

        // Act
        var service = ((IServiceProvider)adapter).GetService(typeof(IGreeter));

        // Assert
        Assert.Null(service);
    }

    [Fact]
    public void IServiceProvider_GetService_ReturnsServiceWhenRegistered()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(scope => new ConsoleGreeter());
        using var adapter = container.CreateServiceProviderScope();

        // Act
        var service = ((IServiceProvider)adapter).GetService(typeof(IGreeter));

        // Assert
        Assert.NotNull(service);
        Assert.IsType<ConsoleGreeter>(service);
    }

    [Fact]
    public void ISvcScope_GetService_ThrowsForUnregistered()
    {
        // Arrange
        using var container = new SvcContainer();
        using var adapter = container.CreateServiceProviderScope();

        // Act & Assert
        Assert.Throws<PicoDiException>(() => ((ISvcScope)adapter).GetService(typeof(IGreeter)));
    }

    [Fact]
    public void GetServiceOrDefault_ReturnsNullForUnregistered()
    {
        // Arrange
        using var container = new SvcContainer();
        using var scope = container.CreateScope();

        // Act
        var service = scope.GetServiceOrDefault<IGreeter>();

        // Assert
        Assert.Null(service);
    }

    [Fact]
    public void GetServiceOrDefault_ReturnsServiceWhenRegistered()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(scope => new ConsoleGreeter());
        using var scope = container.CreateScope();

        // Act
        var service = scope.GetServiceOrDefault<IGreeter>();

        // Assert
        Assert.NotNull(service);
        Assert.IsType<ConsoleGreeter>(service);
    }

    [Fact]
    public void GetRequiredService_ThrowsInvalidOperationForUnregistered()
    {
        // Arrange
        using var container = new SvcContainer();
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => scope.GetRequiredService<IGreeter>()
        );
        Assert.Contains("has been registered", ex.Message);
    }

    [Fact]
    public void GetRequiredService_ReturnsServiceWhenRegistered()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(scope => new ConsoleGreeter());
        using var scope = container.CreateScope();

        // Act
        var service = scope.GetRequiredService<IGreeter>();

        // Assert
        Assert.NotNull(service);
        Assert.IsType<ConsoleGreeter>(service);
    }

    [Fact]
    public void Adapter_CreateScope_ReturnsNewAdapter()
    {
        // Arrange
        using var container = new SvcContainer();
        using var adapter = container.CreateServiceProviderScope();

        // Act
        using var childScope = adapter.CreateScope();

        // Assert
        Assert.IsType<SvcProviderAdapter>(childScope);
        Assert.NotSame(adapter, childScope);
    }

    [Fact]
    public async Task Adapter_DisposeAsync_DisposesScope()
    {
        // Arrange
        var container = new SvcContainer();
        var adapter = container.CreateServiceProviderScope();

        // Act
        await adapter.DisposeAsync();

        // Assert - should throw on access after disposal
        Assert.Throws<ObjectDisposedException>(
            () => ((IServiceProvider)adapter).GetService(typeof(IGreeter))
        );
    }
}
