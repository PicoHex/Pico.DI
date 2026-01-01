namespace Pico.DI.Test;

/// <summary>
/// Tests for nested scopes and scope behavior.
/// </summary>
public class SvcScopeTests : SvcContainerTestBase
{
    [Fact]
    public void Scope_CreateScope_CreatesNestedScope()
    {
        // Arrange
        var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Scoped);

        // Act
        using var scope1 = container.CreateScope();
        using var nestedScope = scope1.CreateScope();

        var greeter1 = scope1.GetService<IGreeter>();
        var greeterNested = nestedScope.GetService<IGreeter>();

        // Assert - Different scopes should have different instances for scoped services
        Assert.NotSame(greeter1, greeterNested);
    }

    [Fact]
    public void Scope_CreateScope_SharesSingletonAcrossNestedScopes()
    {
        // Arrange
        var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Singleton);

        // Act
        using var scope1 = container.CreateScope();
        using var nestedScope = scope1.CreateScope();

        var greeter1 = scope1.GetService<IGreeter>();
        var greeterNested = nestedScope.GetService<IGreeter>();

        // Assert - Singleton should be same across all scopes
        Assert.Same(greeter1, greeterNested);
    }

    [Fact]
    public void Scope_GetService_WithMultipleSingletons_ReturnsSameInstance()
    {
        // Arrange
        var container = new SvcContainer();
        RegisterGreeterPair(container, SvcLifetime.Singleton);

        // Act
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        var greeter1 = scope1.GetService<IGreeter>();
        var greeter2 = scope2.GetService<IGreeter>();

        // Assert - Last registered singleton should be same across scopes
        Assert.Same(greeter1, greeter2);
        Assert.IsType<AlternativeGreeter>(greeter1);
    }

    [Fact]
    public void Scope_GetServices_WithTransient_ReturnsNewInstanceEachTime()
    {
        // Arrange
        var container = new SvcContainer();
        RegisterGreeterPair(container, SvcLifetime.Transient);

        // Act
        using var scope = container.CreateScope();
        var greeters1 = scope.GetServices<IGreeter>().ToList();
        var greeters2 = scope.GetServices<IGreeter>().ToList();

        // Assert - Transient services should be new each time
        Assert.Equal(2, greeters1.Count);
        Assert.Equal(2, greeters2.Count);
        Assert.NotSame(greeters1[0], greeters2[0]);
        Assert.NotSame(greeters1[1], greeters2[1]);
    }

    [Fact]
    public void Scope_GetServices_WithScoped_ReturnsSameInstanceInSameScope()
    {
        // Arrange
        var container = new SvcContainer();
        RegisterGreeterPair(container, SvcLifetime.Scoped);

        // Act
        using var scope = container.CreateScope();
        var greeters1 = scope.GetServices<IGreeter>().ToList();
        var greeters2 = scope.GetServices<IGreeter>().ToList();

        // Assert - Scoped services should be same within same scope
        Assert.Equal(2, greeters1.Count);
        Assert.Equal(2, greeters2.Count);
        Assert.Same(greeters1[0], greeters2[0]);
        Assert.Same(greeters1[1], greeters2[1]);
    }

    [Fact]
    public void Scope_GetServices_WithMixedLifetimes()
    {
        // Arrange
        var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Transient);
        RegisterAlternativeGreeter(container, SvcLifetime.Scoped);

        // Act
        using var scope = container.CreateScope();
        var greeters1 = scope.GetServices<IGreeter>().ToList();
        var greeters2 = scope.GetServices<IGreeter>().ToList();

        // Assert
        Assert.Equal(2, greeters1.Count);

        // Transient should be different
        Assert.NotSame(greeters1[0], greeters2[0]);

        // Scoped should be same
        Assert.Same(greeters1[1], greeters2[1]);
    }
}
