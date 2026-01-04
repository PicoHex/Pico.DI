namespace Pico.DI.Test;

/// <summary>
/// Tests for service lifetime management (Transient, Scoped, Singleton).
/// </summary>
public class SvcScopeLifetimeTests : XUnitTestBase
{
    #region Transient Lifetime

    [Fact]
    public void Transient_CreatesNewInstance_EachRequest()
    {
        // Arrange
        using var container = CreateContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Transient);

        using var scope = container.CreateScope();

        // Act
        var instance1 = scope.GetService<IGreeter>();
        var instance2 = scope.GetService<IGreeter>();

        // Assert
        Assert.NotSame(instance1, instance2);
    }

    [Fact]
    public void Transient_CreatesNewInstance_AcrossScopes()
    {
        // Arrange
        using var container = CreateContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Transient);

        // Act
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();
        var instance1 = scope1.GetService<IGreeter>();
        var instance2 = scope2.GetService<IGreeter>();

        // Assert
        Assert.NotSame(instance1, instance2);
    }

    [Fact]
    public void Transient_WithFactory_InvokesFactoryEachTime()
    {
        // Arrange
        var factoryCallCount = 0;
        using var container = CreateContainer();
        container.RegisterTransient<IGreeter>(_ =>
        {
            Interlocked.Increment(ref factoryCallCount);
            return new ConsoleGreeter();
        });

        using var scope = container.CreateScope();

        // Act
        _ = scope.GetService<IGreeter>();
        _ = scope.GetService<IGreeter>();
        _ = scope.GetService<IGreeter>();

        // Assert
        Assert.Equal(3, factoryCallCount);
    }

    #endregion

    #region Scoped Lifetime

    [Fact]
    public void Scoped_ReturnsSameInstance_WithinScope()
    {
        // Arrange
        using var container = CreateContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Scoped);

        using var scope = container.CreateScope();

        // Act
        var instance1 = scope.GetService<IGreeter>();
        var instance2 = scope.GetService<IGreeter>();

        // Assert
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void Scoped_ReturnsDifferentInstance_AcrossScopes()
    {
        // Arrange
        using var container = CreateContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Scoped);

        // Act
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();
        var instance1 = scope1.GetService<IGreeter>();
        var instance2 = scope2.GetService<IGreeter>();

        // Assert
        Assert.NotSame(instance1, instance2);
    }

    [Fact]
    public void Scoped_NestedScope_HasOwnInstance()
    {
        // Arrange
        using var container = CreateContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Scoped);

        using var parentScope = container.CreateScope();
        using var childScope = parentScope.CreateScope();

        // Act
        var parentInstance = parentScope.GetService<IGreeter>();
        var childInstance = childScope.GetService<IGreeter>();

        // Assert
        Assert.NotSame(parentInstance, childInstance);
    }

    [Fact]
    public void Scoped_WithFactory_InvokesFactoryOncePerScope()
    {
        // Arrange
        var factoryCallCount = 0;
        using var container = CreateContainer();
        container.RegisterScoped<IGreeter>(_ =>
        {
            Interlocked.Increment(ref factoryCallCount);
            return new ConsoleGreeter();
        });

        // Act
        using var scope1 = container.CreateScope();
        _ = scope1.GetService<IGreeter>();
        _ = scope1.GetService<IGreeter>();

        using var scope2 = container.CreateScope();
        _ = scope2.GetService<IGreeter>();

        // Assert
        Assert.Equal(2, factoryCallCount);
    }

    #endregion

    #region Singleton Lifetime

    [Fact]
    public void Singleton_ReturnsSameInstance_WithinScope()
    {
        // Arrange
        using var container = CreateContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Singleton);

        using var scope = container.CreateScope();

        // Act
        var instance1 = scope.GetService<IGreeter>();
        var instance2 = scope.GetService<IGreeter>();

        // Assert
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void Singleton_ReturnsSameInstance_AcrossScopes()
    {
        // Arrange
        using var container = CreateContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Singleton);

        // Act
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();
        var instance1 = scope1.GetService<IGreeter>();
        var instance2 = scope2.GetService<IGreeter>();

        // Assert
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void Singleton_WithFactory_InvokesFactoryOnlyOnce()
    {
        // Arrange
        var factoryCallCount = 0;
        using var container = CreateContainer();
        container.RegisterSingleton<IGreeter>(_ =>
        {
            Interlocked.Increment(ref factoryCallCount);
            return new ConsoleGreeter();
        });

        // Act
        using var scope1 = container.CreateScope();
        _ = scope1.GetService<IGreeter>();

        using var scope2 = container.CreateScope();
        _ = scope2.GetService<IGreeter>();
        _ = scope2.GetService<IGreeter>();

        // Assert
        Assert.Equal(1, factoryCallCount);
    }

    [Fact]
    public void Singleton_ThreadSafe_ConcurrentAccess()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterSingleton<CountingService>(_ => new CountingService());

        var results = new CountingService[100];

        // Act
        Parallel.For(
            0,
            100,
            i =>
            {
                using var scope = container.CreateScope();
                results[i] = scope.GetService<CountingService>();
            }
        );

        // Assert - all should be the same instance (proving thread-safe singleton)
        var first = results[0];
        Assert.All(results, r => Assert.Same(first, r));
    }

    #endregion

    #region RegisterSingle (Instance Registration)

    [Fact]
    public void RegisterSingle_PreExistingInstance_ReturnsExactInstance()
    {
        // Arrange
        using var container = CreateContainer();
        var existingInstance = new ConsoleGreeter();
        container.RegisterSingle<IGreeter>(existingInstance);

        using var scope = container.CreateScope();

        // Act
        var resolved = scope.GetService<IGreeter>();

        // Assert
        Assert.Same(existingInstance, resolved);
    }

    [Fact]
    public void RegisterSingle_ByType_ReturnsExactInstance()
    {
        // Arrange
        using var container = CreateContainer();
        var existingInstance = new ConsoleGreeter();
        container.RegisterSingle(typeof(IGreeter), existingInstance);

        using var scope = container.CreateScope();

        // Act
        var resolved = scope.GetService<IGreeter>();

        // Assert
        Assert.Same(existingInstance, resolved);
    }

    #endregion
}
