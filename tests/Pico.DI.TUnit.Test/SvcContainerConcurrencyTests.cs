namespace Pico.DI.TUnit.Test;

/// <summary>
/// Tests for thread-safety and concurrent access.
/// </summary>
public class SvcContainerConcurrencyTests : TUnitTestBase
{
    #region Concurrent Registration Tests

    [Test]
    public async Task ConcurrentRegistration_BeforeBuild_IsThreadSafe()
    {
        // Arrange
        using var container = new SvcContainer();
        var registrationTasks = new List<Task>();

        // Act - register many services concurrently
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            registrationTasks.Add(
                Task.Run(() =>
                {
                    container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());
                })
            );
        }

        await Task.WhenAll(registrationTasks);
        using var scope = container.CreateScope();

        // Assert - all registrations should be accessible
        var services = scope.GetServices<IGreeter>().ToList();
        await Assert.That(services.Count).IsEqualTo(100);
    }

    #endregion

    #region Concurrent Resolution Tests

    [Test]
    public async Task ConcurrentResolution_Transient_IsThreadSafe()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Transient);

        var resolvedInstances = new System.Collections.Concurrent.ConcurrentBag<IGreeter>();

        // Act
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 100),
            async (_, _) =>
            {
                await using var scope = container.CreateScope();
                var service = scope.GetService<IGreeter>();
                resolvedInstances.Add(service);
            }
        );

        // Assert - all should have resolved successfully
        await Assert.That(resolvedInstances.Count).IsEqualTo(100);
    }

    [Test]
    public async Task ConcurrentResolution_Singleton_ReturnsSameInstance()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Singleton);

        var resolvedInstances = new IGreeter[100];

        // Act
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 100),
            async (i, _) =>
            {
                await using var scope = container.CreateScope();
                resolvedInstances[i] = scope.GetService<IGreeter>();
            }
        );

        // Assert - all should be the same instance
        var first = resolvedInstances[0];
        await Assert.That(resolvedInstances.All(r => ReferenceEquals(r, first))).IsTrue();
    }

    [Test]
    public async Task ConcurrentResolution_Scoped_EachScopeHasOwnInstance()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Scoped);

        var resolvedInstances = new IGreeter[100];

        // Act
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 100),
            async (i, _) =>
            {
                await using var scope = container.CreateScope();
                resolvedInstances[i] = scope.GetService<IGreeter>();
            }
        );

        // Assert - all should be different instances (different scopes)
        var distinct = resolvedInstances.Distinct().Count();
        await Assert.That(distinct).IsEqualTo(100);
    }

    #endregion

    #region Concurrent Scope Creation Tests

    [Test]
    public async Task ConcurrentScopeCreation_IsThreadSafe()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);

        var scopes = new ISvcScope[100];

        // Act
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 100),
            async (i, _) =>
            {
                scopes[i] = container.CreateScope();
                await Task.CompletedTask;
            }
        );

        // Assert - all scopes should be created
        await Assert.That(scopes.All(s => s != null)).IsTrue();

        // Cleanup
        foreach (var scope in scopes)
        {
            scope?.Dispose();
        }
    }

    #endregion

    #region Concurrent GetServices Tests

    [Test]
    public async Task ConcurrentGetServices_IsThreadSafe()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        RegisterAlternativeGreeter(container);

        var results = new List<IGreeter>[100];

        // Act
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 100),
            async (i, _) =>
            {
                await using var scope = container.CreateScope();
                results[i] = scope.GetServices<IGreeter>().ToList();
            }
        );

        // Assert - all should return 2 services
        await Assert.That(results.All(r => r.Count == 2)).IsTrue();
    }

    #endregion

    #region Build Thread Safety Tests

    [Test]
    public async Task ConcurrentResolution_AfterBuild_IsThreadSafe()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Singleton);
        container.Build();

        var resolvedInstances = new IGreeter[100];

        // Act
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 100),
            async (i, _) =>
            {
                await using var scope = container.CreateScope();
                resolvedInstances[i] = scope.GetService<IGreeter>();
            }
        );

        // Assert
        var first = resolvedInstances[0];
        await Assert.That(resolvedInstances.All(r => ReferenceEquals(r, first))).IsTrue();
    }

    #endregion
}
