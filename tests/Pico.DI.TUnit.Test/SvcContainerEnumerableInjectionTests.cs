namespace Pico.DI.TUnit.Test;

/// <summary>
/// Tests for multiple service registrations (GetServices functionality).
/// </summary>
public class SvcContainerEnumerableInjectionTests : TUnitTestBase
{
    #region GetServices Basic Tests

    [Test]
    public async Task GetServices_MultipleRegistrations_ReturnsAll()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        RegisterAlternativeGreeter(container);

        using var scope = container.CreateScope();

        // Act
        var greeters = scope.GetServices<IGreeter>().ToList();

        // Assert
        await Assert.That(greeters.Count).IsEqualTo(2);
        await Assert.That(greeters.Any(g => g is ConsoleGreeter)).IsTrue();
        await Assert.That(greeters.Any(g => g is AlternativeGreeter)).IsTrue();
    }

    [Test]
    public async Task GetServices_SingleRegistration_ReturnsSingleItem()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);

        using var scope = container.CreateScope();

        // Act
        var greeters = scope.GetServices<IGreeter>().ToList();

        // Assert
        await Assert.That(greeters.Count).IsEqualTo(1);
        await Assert.That(greeters[0]).IsTypeOf<ConsoleGreeter>();
    }

    [Test]
    public async Task GetServices_NonGeneric_ReturnsAllInstances()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        RegisterAlternativeGreeter(container);

        using var scope = container.CreateScope();

        // Act
        var greeters = scope.GetServices(typeof(IGreeter)).ToList();

        // Assert
        await Assert.That(greeters.Count).IsEqualTo(2);
    }

    #endregion

    #region GetServices Lifetime Tests

    [Test]
    public async Task GetServices_Transient_CreatesNewInstancesEachCall()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Transient);
        RegisterAlternativeGreeter(container, SvcLifetime.Transient);

        using var scope = container.CreateScope();

        // Act
        var greeters1 = scope.GetServices<IGreeter>().ToList();
        var greeters2 = scope.GetServices<IGreeter>().ToList();

        // Assert
        await Assert.That(greeters1[0]).IsNotSameReferenceAs(greeters2[0]);
        await Assert.That(greeters1[1]).IsNotSameReferenceAs(greeters2[1]);
    }

    [Test]
    public async Task GetServices_Scoped_ReturnsSameInstancesWithinScope()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Scoped);
        RegisterAlternativeGreeter(container, SvcLifetime.Scoped);

        using var scope = container.CreateScope();

        // Act
        var greeters1 = scope.GetServices<IGreeter>().ToList();
        var greeters2 = scope.GetServices<IGreeter>().ToList();

        // Assert
        await Assert.That(greeters1[0]).IsSameReferenceAs(greeters2[0]);
        await Assert.That(greeters1[1]).IsSameReferenceAs(greeters2[1]);
    }

    [Test]
    public async Task GetServices_Singleton_ReturnsSameInstancesGlobally()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Singleton);
        RegisterAlternativeGreeter(container, SvcLifetime.Singleton);

        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        // Act
        var greeters1 = scope1.GetServices<IGreeter>().ToList();
        var greeters2 = scope2.GetServices<IGreeter>().ToList();

        // Assert
        await Assert.That(greeters1[0]).IsSameReferenceAs(greeters2[0]);
        await Assert.That(greeters1[1]).IsSameReferenceAs(greeters2[1]);
    }

    #endregion

    #region GetService Last Registration Wins

    [Test]
    public async Task GetService_MultipleRegistrations_ReturnsLastRegistered()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        RegisterAlternativeGreeter(container);

        using var scope = container.CreateScope();

        // Act
        var greeter = scope.GetService<IGreeter>();

        // Assert
        await Assert.That(greeter).IsTypeOf<AlternativeGreeter>();
    }

    [Test]
    public async Task GetService_OverrideRegistration_LastWins()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        RegisterConsoleGreeter(container); // Register same type again
        RegisterAlternativeGreeter(container);

        using var scope = container.CreateScope();

        // Act
        var greeter = scope.GetService<IGreeter>();
        var allGreeters = scope.GetServices<IGreeter>().ToList();

        // Assert
        await Assert.That(greeter).IsTypeOf<AlternativeGreeter>();
        await Assert.That(allGreeters.Count).IsEqualTo(3);
    }

    #endregion

    #region RegisterRange Tests

    [Test]
    public async Task RegisterRange_RegistersAllDescriptors()
    {
        // Arrange
        using var container = new SvcContainer();
        var descriptors = new[]
        {
            new SvcDescriptor(typeof(IGreeter), _ => new ConsoleGreeter(), SvcLifetime.Transient),
            new SvcDescriptor(typeof(ILogger), _ => new ConsoleLogger(), SvcLifetime.Transient)
        };

        // Act
        container.RegisterRange(descriptors);
        using var scope = container.CreateScope();

        // Assert
        var greeter = scope.GetService<IGreeter>();
        var logger = scope.GetService<ILogger>();
        await Assert.That(greeter).IsNotNull();
        await Assert.That(logger).IsNotNull();
    }

    [Test]
    public async Task RegisterRange_MultipleOfSameType_AllRegistered()
    {
        // Arrange
        using var container = new SvcContainer();
        var descriptors = new[]
        {
            new SvcDescriptor(typeof(IGreeter), _ => new ConsoleGreeter(), SvcLifetime.Transient),
            new SvcDescriptor(
                typeof(IGreeter),
                _ => new AlternativeGreeter(),
                SvcLifetime.Transient
            )
        };

        // Act
        container.RegisterRange(descriptors);
        using var scope = container.CreateScope();

        // Assert
        var greeters = scope.GetServices<IGreeter>().ToList();
        await Assert.That(greeters.Count).IsEqualTo(2);
    }

    [Test]
    public async Task RegisterRange_ReturnsContainerForChaining()
    {
        // Arrange
        using var container = new SvcContainer();
        var descriptors = new[]
        {
            new SvcDescriptor(typeof(IGreeter), _ => new ConsoleGreeter(), SvcLifetime.Transient)
        };

        // Act
        var result = container.RegisterRange(descriptors);

        // Assert
        await Assert.That(result).IsSameReferenceAs(container);
    }

    #endregion
}
