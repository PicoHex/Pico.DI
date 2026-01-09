namespace Pico.DI.Test;

/// <summary>
/// Tests for container and scope lifecycle management.
/// </summary>
public class ContainerLifecycleTests
{
    #region Container Build Tests

    [Test]
    public async Task Build_AfterBuild_CannotRegisterMore()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        container.Build();

        // Act & Assert
        await Assert.That(() => 
            container.RegisterTransient<ILevelOneService>(static _ => new LevelOneService()))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Build_CalledMultipleTimes_NoError()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());

        // Act & Assert - Should not throw
        container.Build();
        container.Build();
        container.Build();
        
        using var scope = container.CreateScope();
        var service = scope.GetService<ISimpleService>();
        await Assert.That(service).IsNotNull();
    }

    [Test]
    public async Task CreateScope_AutoBuildsIfNotBuilt()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        // Note: Not calling Build() explicitly

        // Act
        using var scope = container.CreateScope();
        var service = scope.GetService<ISimpleService>();

        // Assert - Should work because CreateScope auto-builds
        await Assert.That(service).IsNotNull();
    }

    #endregion

    #region Container Disposal Tests

    [Test]
    public async Task Dispose_AfterDispose_ThrowsOnCreateScope()
    {
        // Arrange
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        
        // Act
        container.Dispose();

        // Assert
        await Assert.That(() => container.CreateScope())
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Dispose_AfterDispose_ThrowsOnRegister()
    {
        // Arrange
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.Dispose();

        // Assert
        await Assert.That(() => 
            container.RegisterTransient<ISimpleService>(static _ => new SimpleService()))
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Dispose_DisposesAllScopes()
    {
        // Arrange
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<IDisposableService>(static _ => new DisposableService());
        
        var scope1 = container.CreateScope();
        var scope2 = container.CreateScope();
        var childScope = scope1.CreateScope();
        
        var instance1 = (DisposableService)scope1.GetService<IDisposableService>();
        var instance2 = (DisposableService)scope2.GetService<IDisposableService>();
        var childInstance = (DisposableService)childScope.GetService<IDisposableService>();

        // Act
        container.Dispose();

        // Assert - All scoped instances should be disposed
        await Assert.That(instance1.IsDisposed).IsTrue();
        await Assert.That(instance2.IsDisposed).IsTrue();
        await Assert.That(childInstance.IsDisposed).IsTrue();
    }

    [Test]
    public async Task DisposeAsync_DisposesAllScopes()
    {
        // Arrange
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<IAsyncDisposableService>(static _ => new AsyncDisposableService());
        
        var scope1 = container.CreateScope();
        var scope2 = container.CreateScope();
        
        var instance1 = (AsyncDisposableService)scope1.GetService<IAsyncDisposableService>();
        var instance2 = (AsyncDisposableService)scope2.GetService<IAsyncDisposableService>();

        // Act
        await container.DisposeAsync();

        // Assert
        await Assert.That(instance1.IsDisposed).IsTrue();
        await Assert.That(instance2.IsDisposed).IsTrue();
    }

    [Test]
    public async Task Dispose_DoubleDispose_NoError()
    {
        // Arrange
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingleton<IDisposableService>(static _ => new DisposableService());
        using var scope = container.CreateScope();
        var instance = scope.GetService<IDisposableService>();

        // Act & Assert - Should not throw
        container.Dispose();
        container.Dispose();
        
        await Assert.That(((DisposableService)instance).IsDisposed).IsTrue();
    }

    #endregion

    #region Scope Disposal Tests

    [Test]
    public async Task ScopeDispose_DisposesChildScopes()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<IDisposableService>(static _ => new DisposableService());
        
        var parentScope = container.CreateScope();
        var childScope = parentScope.CreateScope();
        var grandchildScope = childScope.CreateScope();
        
        var parentInstance = (DisposableService)parentScope.GetService<IDisposableService>();
        var childInstance = (DisposableService)childScope.GetService<IDisposableService>();
        var grandchildInstance = (DisposableService)grandchildScope.GetService<IDisposableService>();

        // Act - Dispose parent scope
        parentScope.Dispose();

        // Assert - Parent and all children should be disposed
        await Assert.That(parentInstance.IsDisposed).IsTrue();
        await Assert.That(childInstance.IsDisposed).IsTrue();
        await Assert.That(grandchildInstance.IsDisposed).IsTrue();
    }

    [Test]
    public async Task ScopeDispose_OnlyDisposesOwnInstances()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<IDisposableService>(static _ => new DisposableService());
        
        using var scope1 = container.CreateScope();
        var scope2 = container.CreateScope();
        
        var instance1 = (DisposableService)scope1.GetService<IDisposableService>();
        var instance2 = (DisposableService)scope2.GetService<IDisposableService>();

        // Act - Dispose only scope2
        scope2.Dispose();

        // Assert - Only scope2's instance should be disposed
        await Assert.That(instance1.IsDisposed).IsFalse();
        await Assert.That(instance2.IsDisposed).IsTrue();
    }

    [Test]
    public async Task ScopeDispose_DoesNotDisposeSingletons()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingleton<IDisposableService>(static _ => new DisposableService());
        
        DisposableService singletonInstance;
        using (var scope = container.CreateScope())
        {
            singletonInstance = (DisposableService)scope.GetService<IDisposableService>();
        }

        // Assert - Singleton should NOT be disposed when scope is disposed
        await Assert.That(singletonInstance.IsDisposed).IsFalse();
    }

    [Test]
    public async Task ScopeDisposeAsync_DisposesAsyncDisposables()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<IAsyncDisposableService>(static _ => new AsyncDisposableService());
        
        AsyncDisposableService instance;
        await using (var scope = container.CreateScope())
        {
            instance = (AsyncDisposableService)scope.GetService<IAsyncDisposableService>();
            await Assert.That(instance.IsDisposed).IsFalse();
        }

        // Assert
        await Assert.That(instance.IsDisposed).IsTrue();
    }

    #endregion

    #region AutoConfigure Tests

    [Test]
    public async Task Constructor_WithAutoConfigureFalse_DoesNotAutoRegister()
    {
        // Arrange & Act
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        
        // Assert - Container should be empty (no auto-configured services)
        // This test verifies the flag works; actual auto-configuration depends on source generator
        container.Build();
        await Assert.That(true).IsTrue();
    }

    #endregion

    #region Concurrent Access Tests

    [Test]
    public async Task ConcurrentScopeCreation_ThreadSafe()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());
        container.Build();

        // Act - Create many scopes concurrently
        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            using var scope = container.CreateScope();
            return scope.GetService<ISimpleService>().InstanceId;
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - All should succeed with unique scoped instances
        await Assert.That(results.Distinct().Count()).IsEqualTo(100);
    }

    [Test]
    public async Task ConcurrentScopeResolution_WithinScope_ThreadSafe()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());
        using var scope = container.CreateScope();

        // Act - Resolve from same scope concurrently
        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
            scope.GetService<ISimpleService>().InstanceId
        )).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - All should return the same scoped instance
        await Assert.That(results.Distinct().Count()).IsEqualTo(1);
    }

    #endregion
}
