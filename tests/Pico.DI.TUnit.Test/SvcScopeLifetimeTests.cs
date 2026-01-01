namespace Pico.DI.TUnit.Test;

/// <summary>
/// Tests for SvcScope lifetime management.
/// </summary>
public class SvcScopeLifetimeTests : TUnitTestBase
{
    #region Transient Lifetime

    [Test]
    public async Task Transient_CreatesNewInstance_EachRequest()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Transient);

        using var scope = container.CreateScope();

        // Act
        var instance1 = scope.GetService<IGreeter>();
        var instance2 = scope.GetService<IGreeter>();

        // Assert
        await Assert.That(instance1).IsNotSameReferenceAs(instance2);
    }

    [Test]
    public async Task Transient_CreatesNewInstance_AcrossScopes()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Transient);

        // Act
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();
        var instance1 = scope1.GetService<IGreeter>();
        var instance2 = scope2.GetService<IGreeter>();

        // Assert
        await Assert.That(instance1).IsNotSameReferenceAs(instance2);
    }

    #endregion

    #region Scoped Lifetime

    [Test]
    public async Task Scoped_ReturnsSameInstance_WithinScope()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Scoped);

        using var scope = container.CreateScope();

        // Act
        var instance1 = scope.GetService<IGreeter>();
        var instance2 = scope.GetService<IGreeter>();

        // Assert
        await Assert.That(instance1).IsSameReferenceAs(instance2);
    }

    [Test]
    public async Task Scoped_ReturnsDifferentInstance_AcrossScopes()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Scoped);

        // Act
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();
        var instance1 = scope1.GetService<IGreeter>();
        var instance2 = scope2.GetService<IGreeter>();

        // Assert
        await Assert.That(instance1).IsNotSameReferenceAs(instance2);
    }

    [Test]
    public async Task Scoped_NestedScope_HasOwnInstance()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Scoped);

        using var parentScope = container.CreateScope();
        using var childScope = parentScope.CreateScope();

        // Act
        var parentInstance = parentScope.GetService<IGreeter>();
        var childInstance = childScope.GetService<IGreeter>();

        // Assert
        await Assert.That(parentInstance).IsNotSameReferenceAs(childInstance);
    }

    #endregion

    #region Singleton Lifetime

    [Test]
    public async Task Singleton_ReturnsSameInstance_WithinScope()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Singleton);

        using var scope = container.CreateScope();

        // Act
        var instance1 = scope.GetService<IGreeter>();
        var instance2 = scope.GetService<IGreeter>();

        // Assert
        await Assert.That(instance1).IsSameReferenceAs(instance2);
    }

    [Test]
    public async Task Singleton_ReturnsSameInstance_AcrossScopes()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Singleton);

        // Act
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();
        var instance1 = scope1.GetService<IGreeter>();
        var instance2 = scope2.GetService<IGreeter>();

        // Assert
        await Assert.That(instance1).IsSameReferenceAs(instance2);
    }

    [Test]
    public async Task Singleton_ThreadSafe_ConcurrentAccess()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Singleton);

        var results = new IGreeter[10];

        // Act
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 10),
            async (i, _) =>
            {
                await using var scope = container.CreateScope();
                results[i] = scope.GetService<IGreeter>();
            }
        );

        // Assert
        var first = results[0];
        await Assert.That(results.All(r => ReferenceEquals(r, first))).IsTrue();
    }

    #endregion

    #region Mixed Lifetimes

    [Test]
    public async Task MixedLifetimes_TransientAndSingleton()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Singleton);
        RegisterConsoleLogger(container, SvcLifetime.Transient);

        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        // Act
        var greeter1 = scope1.GetService<IGreeter>();
        var greeter2 = scope2.GetService<IGreeter>();
        var logger1 = scope1.GetService<ILogger>();
        var logger2 = scope1.GetService<ILogger>();

        // Assert
        await Assert.That(greeter1).IsSameReferenceAs(greeter2);
        await Assert.That(logger1).IsNotSameReferenceAs(logger2);
    }

    [Test]
    public async Task MixedLifetimes_ScopedAndSingleton()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Singleton);
        RegisterConsoleLogger(container, SvcLifetime.Scoped);

        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        // Act
        var greeter1 = scope1.GetService<IGreeter>();
        var greeter2 = scope2.GetService<IGreeter>();
        var logger1 = scope1.GetService<ILogger>();
        var logger2 = scope2.GetService<ILogger>();
        var logger1Again = scope1.GetService<ILogger>();

        // Assert
        await Assert.That(greeter1).IsSameReferenceAs(greeter2);
        await Assert.That(logger1).IsNotSameReferenceAs(logger2);
        await Assert.That(logger1).IsSameReferenceAs(logger1Again);
    }

    #endregion
}
