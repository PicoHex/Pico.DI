namespace Pico.DI.TUnit.Test;

/// <summary>
/// Tests for ISvcScope interface and extension methods.
/// </summary>
public class ISvcScopeTests : TUnitTestBase
{
    #region GetService Generic Extension Tests

    [Test]
    public async Task GetService_Generic_ReturnsTypedInstance()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        using var scope = container.CreateScope();

        // Act
        var greeter = scope.GetService<IGreeter>();

        // Assert
        await Assert.That(greeter).IsNotNull();
        await Assert.That(greeter).IsTypeOf<ConsoleGreeter>();
    }

    [Test]
    public async Task GetService_NonGeneric_ReturnsObjectInstance()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        using var scope = container.CreateScope();

        // Act
        var greeter = scope.GetService(typeof(IGreeter));

        // Assert
        await Assert.That(greeter).IsNotNull();
        await Assert.That(greeter).IsTypeOf<ConsoleGreeter>();
    }

    #endregion

    #region GetServices Generic Extension Tests

    [Test]
    public async Task GetServices_Generic_ReturnsTypedEnumerable()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        RegisterAlternativeGreeter(container);
        using var scope = container.CreateScope();

        // Act
        var greeters = scope.GetServices<IGreeter>();

        // Assert
        await Assert.That(greeters).IsNotNull();
        var list = greeters.ToList();
        await Assert.That(list.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GetServices_NonGeneric_ReturnsObjectEnumerable()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        using var scope = container.CreateScope();

        // Act
        var greeters = scope.GetServices(typeof(IGreeter));

        // Assert
        await Assert.That(greeters).IsNotNull();
        await Assert.That(greeters.Count()).IsEqualTo(1);
    }

    #endregion

    #region CreateScope Extension Tests

    [Test]
    public async Task CreateScope_ReturnsNewScope()
    {
        // Arrange
        using var container = new SvcContainer();
        using var scope = container.CreateScope();

        // Act
        using var nestedScope = scope.CreateScope();

        // Assert
        await Assert.That(nestedScope).IsNotNull();
        await Assert.That(nestedScope).IsNotSameReferenceAs(scope);
    }

    #endregion

    #region Type Cast Tests

    [Test]
    public async Task GetService_Generic_CastsCorrectly()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        using var scope = container.CreateScope();

        // Act
        IGreeter greeter = scope.GetService<IGreeter>();

        // Assert - can call interface methods
        var result = greeter.Greet("World");
        await Assert.That(result).IsEqualTo("Hello, World!");
    }

    [Test]
    public async Task GetServices_Generic_CastsCorrectly()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        RegisterAlternativeGreeter(container);
        using var scope = container.CreateScope();

        // Act
        var greeters = scope.GetServices<IGreeter>().ToList();

        // Assert - can call interface methods on each
        var messages = greeters.Select(g => g.Greet("World")).ToList();
        await Assert.That(messages).Contains("Hello, World!");
        await Assert.That(messages).Contains("Hi, World!");
    }

    #endregion
}
