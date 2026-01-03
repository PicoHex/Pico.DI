namespace Pico.DI.Test;

/// <summary>
/// Tests for thread-safety and concurrent access.
/// </summary>
public class SvcContainerConcurrencyTests : XUnitTestBase
{
    #region Concurrent Registration Tests

    [Fact]
    public async Task ConcurrentRegistration_BeforeBuild_IsThreadSafe()
    {
        // Arrange
        using var container = new SvcContainer();
        var registrationTasks = new List<Task>();

        // Act - register many services concurrently
        for (int i = 0; i < 100; i++)
        {
            registrationTasks.Add(
                Task.Run(
                    () =>
                    {
                        container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());
                    },
                    TestContext.Current.CancellationToken
                )
            );
        }

        await Task.WhenAll(registrationTasks);
        using var scope = container.CreateScope();

        // Assert - all registrations should be accessible
        var services = scope.GetServices<IGreeter>().ToList();
        Assert.Equal(100, services.Count);
    }

    #endregion

    #region Concurrent Resolution Tests

    [Fact]
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
        Assert.Equal(100, resolvedInstances.Count);
    }

    [Fact]
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
        Assert.All(resolvedInstances, r => Assert.Same(first, r));
    }

    [Fact]
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

        // Assert - all should be different instances (each scope has its own)
        var uniqueInstances = resolvedInstances.Distinct().Count();
        Assert.Equal(100, uniqueInstances);
    }

    [Fact]
    public async Task ConcurrentResolution_WithinSameScope_Scoped_ReturnsSame()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Scoped);

        using var scope = container.CreateScope();
        var resolvedInstances = new IGreeter[100];

        // Act
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 100),
            async (i, _) =>
            {
                await Task.Yield();
                resolvedInstances[i] = scope.GetService<IGreeter>();
            }
        );

        // Assert - all should be the same instance (within same scope)
        var first = resolvedInstances[0];
        Assert.All(resolvedInstances, r => Assert.Same(first, r));
    }

    #endregion

    #region Concurrent Registration and Resolution Tests

    [Fact]
    public async Task ConcurrentResolution_WhileBuilding_IsThreadSafe()
    {
        // Arrange
        using var container = new SvcContainer();
        for (int i = 0; i < 50; i++)
        {
            container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());
        }

        var resolutionTasks = new List<Task<IGreeter>>();

        // Act - try to resolve while building
        var buildTask = Task.Run(
            () =>
            {
                container.Build();
            },
            TestContext.Current.CancellationToken
        );

        for (int i = 0; i < 100; i++)
        {
            resolutionTasks.Add(
                Task.Run(() =>
                {
                    using var scope = container.CreateScope();
                    return scope.GetService<IGreeter>();
                })
            );
        }

        await buildTask;
        var results = await Task.WhenAll(resolutionTasks);

        // Assert - all should resolve without error
        Assert.All(results, r => Assert.NotNull(r));
    }

    #endregion

    #region Concurrent Scope Operations Tests

    [Fact]
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
                await Task.Yield();
                scopes[i] = container.CreateScope();
            }
        );

        // Assert - all scopes should be distinct
        var uniqueScopes = scopes.Distinct().Count();
        Assert.Equal(100, uniqueScopes);

        // Cleanup
        foreach (var scope in scopes)
        {
            scope.Dispose();
        }
    }

    [Fact]
    public async Task ConcurrentScopeDisposal_IsThreadSafe()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterScoped<DisposableService>(_ => new DisposableService());

        var scopes = new ISvcScope[100];
        var services = new DisposableService[100];

        for (int i = 0; i < 100; i++)
        {
            scopes[i] = container.CreateScope();
            services[i] = scopes[i].GetService<DisposableService>();
        }

        // Act - dispose all scopes concurrently
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 100),
            async (i, _) =>
            {
                await Task.Yield();
                scopes[i].Dispose();
            }
        );

        // Assert - all services should be disposed
        Assert.All(services, s => Assert.True(s.IsDisposed));
    }

    #endregion

    #region Singleton Thread Safety Tests

    [Fact]
    public void Singleton_ConcurrentCreation_OnlyCreatesOnce()
    {
        // Arrange
        var creationCount = 0;
        using var container = new SvcContainer();
        container.RegisterSingleton<IGreeter>(_ =>
        {
            Interlocked.Increment(ref creationCount);
            Thread.Sleep(10); // Simulate slow construction
            return new ConsoleGreeter();
        });

        var results = new IGreeter[100];

        // Act
        Parallel.For(
            0,
            100,
            i =>
            {
                using var scope = container.CreateScope();
                results[i] = scope.GetService<IGreeter>();
            }
        );

        // Assert - factory should only be called once
        Assert.Equal(1, creationCount);
        Assert.All(results, r => Assert.Same(results[0], r));
    }

    #endregion
}
