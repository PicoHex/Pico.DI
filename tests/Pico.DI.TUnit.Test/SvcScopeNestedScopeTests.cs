namespace Pico.DI.TUnit.Test;

/// <summary>
/// Tests for nested scope creation and service resolution.
/// </summary>
public class SvcScopeNestedScopeTests : TUnitTestBase
{
    #region CreateScope Tests

    [Test]
    public async Task CreateScope_FromScope_CreatesNestedScope()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        using var parentScope = container.CreateScope();

        // Act
        using var childScope = parentScope.CreateScope();

        // Assert
        await Assert.That(childScope).IsNotNull();
        await Assert.That(childScope).IsNotSameReferenceAs(parentScope);
    }

    [Test]
    public async Task CreateScope_Nested_CanResolveServices()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        using var parentScope = container.CreateScope();
        using var childScope = parentScope.CreateScope();

        // Act
        var greeter = childScope.GetService<IGreeter>();

        // Assert
        await Assert.That(greeter).IsNotNull();
        await Assert.That(greeter).IsTypeOf<ConsoleGreeter>();
    }

    [Test]
    public async Task CreateScope_MultipleNested_AllCanResolve()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        using var scope1 = container.CreateScope();
        using var scope2 = scope1.CreateScope();
        using var scope3 = scope2.CreateScope();

        // Act
        var greeter = scope3.GetService<IGreeter>();

        // Assert
        await Assert.That(greeter).IsNotNull();
    }

    #endregion

    #region Scoped Lifetime in Nested Scopes

    [Test]
    public async Task Scoped_NestedScope_HasOwnInstances()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Scoped);

        using var parentScope = container.CreateScope();
        using var childScope = parentScope.CreateScope();

        // Act
        var parentGreeter = parentScope.GetService<IGreeter>();
        var childGreeter = childScope.GetService<IGreeter>();

        // Assert - each scope has its own scoped instance
        await Assert.That(parentGreeter).IsNotSameReferenceAs(childGreeter);
    }

    [Test]
    public async Task Scoped_WithinNestedScope_SameInstance()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Scoped);

        using var parentScope = container.CreateScope();
        using var childScope = parentScope.CreateScope();

        // Act
        var instance1 = childScope.GetService<IGreeter>();
        var instance2 = childScope.GetService<IGreeter>();

        // Assert
        await Assert.That(instance1).IsSameReferenceAs(instance2);
    }

    #endregion

    #region Singleton Lifetime in Nested Scopes

    [Test]
    public async Task Singleton_NestedScope_SameInstanceAsParent()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Singleton);

        using var parentScope = container.CreateScope();
        using var childScope = parentScope.CreateScope();

        // Act
        var parentGreeter = parentScope.GetService<IGreeter>();
        var childGreeter = childScope.GetService<IGreeter>();

        // Assert - singleton is shared across all scopes
        await Assert.That(parentGreeter).IsSameReferenceAs(childGreeter);
    }

    [Test]
    public async Task Singleton_DeeplyNestedScope_SameInstance()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Singleton);

        using var scope1 = container.CreateScope();
        using var scope2 = scope1.CreateScope();
        using var scope3 = scope2.CreateScope();
        using var scope4 = scope3.CreateScope();

        // Act
        var greeter1 = scope1.GetService<IGreeter>();
        var greeter4 = scope4.GetService<IGreeter>();

        // Assert
        await Assert.That(greeter1).IsSameReferenceAs(greeter4);
    }

    #endregion

    #region Transient Lifetime in Nested Scopes

    [Test]
    public async Task Transient_NestedScope_AlwaysNewInstance()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Transient);

        using var parentScope = container.CreateScope();
        using var childScope = parentScope.CreateScope();

        // Act
        var parentGreeter = parentScope.GetService<IGreeter>();
        var childGreeter = childScope.GetService<IGreeter>();

        // Assert
        await Assert.That(parentGreeter).IsNotSameReferenceAs(childGreeter);
    }

    #endregion

    #region Disposal Tests

    [Test]
    public async Task ChildScope_Disposal_DoesNotAffectParent()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);

        using var parentScope = container.CreateScope();
        var childScope = parentScope.CreateScope();

        // Act
        childScope.Dispose();

        // Assert - parent still works
        var greeter = parentScope.GetService<IGreeter>();
        await Assert.That(greeter).IsNotNull();
    }

    [Test]
    public async Task ChildScope_Disposal_DisposesOwnScopedInstances()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterScoped<DisposableService>(_ => new DisposableService());

        using var parentScope = container.CreateScope();
        DisposableService? childService;
        using (var childScope = parentScope.CreateScope())
        {
            childService = childScope.GetService<DisposableService>();
            await Assert.That(childService.IsDisposed).IsFalse();
        }

        // Assert - child's scoped service is disposed
        await Assert.That(childService.IsDisposed).IsTrue();
    }

    #endregion
}
