namespace Pico.DI.Test;

/// <summary>
/// Tests for multiple service registrations (enumerable injection).
/// </summary>
public class SvcContainerEnumerableInjectionTests : XUnitTestBase
{
    #region Multiple Registrations Tests

    [Fact]
    public void GetServices_MultipleRegistrations_ReturnsAll()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());
        container.RegisterTransient<IGreeter>(_ => new AlternativeGreeter());

        using var scope = container.CreateScope();

        // Act
        var services = scope.GetServices<IGreeter>().ToList();

        // Assert
        Assert.Equal(2, services.Count);
    }

    [Fact]
    public void GetServices_MultipleRegistrations_PreservesOrder()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());
        container.RegisterTransient<IGreeter>(_ => new AlternativeGreeter());

        using var scope = container.CreateScope();

        // Act
        var services = scope.GetServices<IGreeter>().ToList();

        // Assert - should return in registration order
        Assert.IsType<ConsoleGreeter>(services[0]);
        Assert.IsType<AlternativeGreeter>(services[1]);
    }

    [Fact]
    public void GetService_MultipleRegistrations_ReturnsLast()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());
        container.RegisterTransient<IGreeter>(_ => new AlternativeGreeter());

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IGreeter>();

        // Assert - should return last registration
        Assert.IsType<AlternativeGreeter>(service);
    }

    #endregion

    #region Mixed Lifetime Multiple Registrations Tests

    [Fact]
    public void GetServices_MixedLifetimes_AllResolve()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterSingleton<IGreeter>(_ => new ConsoleGreeter());
        container.RegisterScoped<IGreeter>(_ => new AlternativeGreeter());

        using var scope = container.CreateScope();

        // Act
        var services = scope.GetServices<IGreeter>().ToList();

        // Assert
        Assert.Equal(2, services.Count);
    }

    [Fact]
    public void GetServices_MixedLifetimes_RespectsLifetimes()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterSingleton<IGreeter>(_ => new ConsoleGreeter());
        container.RegisterTransient<IGreeter>(_ => new AlternativeGreeter());

        // Act
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        var services1 = scope1.GetServices<IGreeter>().ToList();
        var services2 = scope2.GetServices<IGreeter>().ToList();

        // Assert - singleton should be same, transient should be different
        Assert.Same(services1[0], services2[0]); // Singleton
        Assert.NotSame(services1[1], services2[1]); // Transient
    }

    #endregion

    #region Factory with Enumerable Dependencies Tests

    [Fact]
    public void Factory_CanResolve_MultipleServices()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());
        container.RegisterTransient<IGreeter>(_ => new AlternativeGreeter());
        container.RegisterTransient<GreeterAggregator>(
            scope => new GreeterAggregator(scope.GetServices<IGreeter>())
        );

        using var scope = container.CreateScope();

        // Act
        var aggregator = scope.GetService<GreeterAggregator>();

        // Assert
        Assert.Equal(2, aggregator.Greeters.Count());
    }

    // Helper class for testing enumerable injection
    public class GreeterAggregator(IEnumerable<IGreeter> greeters)
    {
        public IEnumerable<IGreeter> Greeters { get; } = greeters;

        public string GreetAll(string name) =>
            string.Join(", ", Greeters.Select(g => g.Greet(name)));
    }

    #endregion

    #region RegisterRange Tests

    [Fact]
    public void RegisterRange_WithMultipleDescriptors_AllResolvable()
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
            ),
            new SvcDescriptor(typeof(ILogger), _ => new ConsoleLogger(), SvcLifetime.Singleton)
        };

        // Act
        container.RegisterRange(descriptors);

        using var scope = container.CreateScope();

        // Assert
        Assert.Equal(2, scope.GetServices<IGreeter>().Count());
        Assert.NotNull(scope.GetService<ILogger>());
    }

    [Fact]
    public void RegisterRange_EmptyCollection_DoesNotThrow()
    {
        // Arrange
        using var container = new SvcContainer();
        var descriptors = Array.Empty<SvcDescriptor>();

        // Act - should not throw
        container.RegisterRange(descriptors);

        // Assert - if we got here, no exception was thrown
        Assert.NotNull(container);
    }

    [Fact]
    public void RegisterRange_ReturnsContainer_ForChaining()
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
        Assert.Same(container, result);
    }

    #endregion
}
