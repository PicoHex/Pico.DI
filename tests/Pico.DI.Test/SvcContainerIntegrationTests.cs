namespace Pico.DI.Test;

/// <summary>
/// Integration tests covering complex scenarios and real-world usage patterns.
/// </summary>
public class SvcContainerIntegrationTests : XUnitTestBase
{
    #region Complex Dependency Graph Tests

    [Fact]
    public void Integration_ComplexDependencyGraph_ResolvesCorrectly()
    {
        // Arrange - set up a complex dependency graph
        using var container = CreateContainer();

        container.RegisterSingleton<ILogger>(_ => new ConsoleLogger());
        container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());
        container.RegisterScoped<ServiceWithDependency>(
            scope => new ServiceWithDependency(scope.GetService<IGreeter>())
        );
        container.RegisterTransient<ServiceWithMultipleDependencies>(
            scope =>
                new ServiceWithMultipleDependencies(
                    scope.GetService<IGreeter>(),
                    scope.GetService<ILogger>()
                )
        );

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<ServiceWithMultipleDependencies>();

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.Greeter);
        Assert.NotNull(service.Logger);
    }

    [Fact]
    public void Integration_NestedDependencies_ResolvesSameScope()
    {
        // Arrange
        using var container = CreateContainer();

        container.RegisterScoped<ILogger>(_ => new ConsoleLogger());
        container.RegisterScoped<IGreeter>(_ => new ConsoleGreeter());
        container.RegisterScoped<ServiceWithDependency>(
            scope => new ServiceWithDependency(scope.GetService<IGreeter>())
        );
        container.RegisterScoped<ServiceWithMultipleDependencies>(
            scope =>
                new ServiceWithMultipleDependencies(
                    scope.GetService<IGreeter>(),
                    scope.GetService<ILogger>()
                )
        );

        using var scope = container.CreateScope();

        // Act
        var service1 = scope.GetService<ServiceWithDependency>();
        var service2 = scope.GetService<ServiceWithMultipleDependencies>();

        // Assert - both should use same scoped instances
        Assert.Same(service1.Greeter, service2.Greeter);
    }

    #endregion

    #region Lifecycle Tests

    [Fact]
    public void Integration_FullLifecycle_WorksCorrectly()
    {
        // Arrange
        DisposableService? singletonService;

        using (var container = CreateContainer())
        {
            container.RegisterSingleton<DisposableService>(_ => new DisposableService());

            using (var scope = container.CreateScope())
            {
                singletonService = scope.GetService<DisposableService>();

                // Assert - not disposed during active scope
                Assert.False(singletonService.IsDisposed);
            }

            // After scope disposal, singleton should still be alive
            Assert.False(singletonService.IsDisposed);
        }

        // After container disposal, singleton should be disposed
        Assert.True(singletonService.IsDisposed);
    }

    [Fact]
    public async Task Integration_AsyncLifecycle_WorksCorrectly()
    {
        // Arrange
        AsyncDisposableService? service;

        await using (var container = CreateContainer())
        {
            container.RegisterSingleton<AsyncDisposableService>(_ => new AsyncDisposableService());

            await using (var scope = container.CreateScope())
            {
                service = scope.GetService<AsyncDisposableService>();
                Assert.False(service.IsDisposed);
            }

            // After scope disposal, singleton should still be alive
            Assert.False(service.IsDisposed);
        }

        // After container disposal, should be disposed
        Assert.True(service.IsDisposed);
    }

    #endregion

    #region Method Chaining Tests

    [Fact]
    public void Integration_FluentRegistration_WorksCorrectly()
    {
        // Arrange & Act
        using var container = CreateContainer();
        container
            .RegisterSingleton<ILogger>(_ => new ConsoleLogger())
            .RegisterTransient<IGreeter>(_ => new ConsoleGreeter())
            .RegisterScoped<ServiceWithDependency>(
                scope => new ServiceWithDependency(scope.GetService<IGreeter>())
            );
        container.Build();

        using var scope = container.CreateScope();

        // Assert
        Assert.NotNull(scope.GetService<ILogger>());
        Assert.NotNull(scope.GetService<IGreeter>());
        Assert.NotNull(scope.GetService<ServiceWithDependency>());
    }

    #endregion

    #region Override Pattern Tests

    [Fact]
    public void Integration_OverridePattern_LastRegistrationWins()
    {
        // Arrange
        using var container = CreateContainer();

        // First registration
        container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());

        // Override with different implementation
        container.RegisterTransient<IGreeter>(_ => new AlternativeGreeter());

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IGreeter>();

        // Assert - last registration should win
        Assert.IsType<AlternativeGreeter>(service);
    }

    [Fact]
    public void Integration_OverridePattern_GetServicesReturnsAll()
    {
        // Arrange
        using var container = CreateContainer();

        container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());
        container.RegisterTransient<IGreeter>(_ => new AlternativeGreeter());

        using var scope = container.CreateScope();

        // Act
        var services = scope.GetServices<IGreeter>().ToList();

        // Assert - all registrations should be available
        Assert.Equal(2, services.Count);
    }

    #endregion

    #region Decorator Pattern Tests

    [Fact]
    public void Integration_DecoratorPattern_WorksWithFactories()
    {
        // Arrange
        using var container = CreateContainer();

        // Register the base implementation with a concrete type
        container.RegisterTransient<ConsoleGreeter>(_ => new ConsoleGreeter());

        // Register the decorator that wraps the base - reference the concrete type to avoid recursion
        container.RegisterTransient<IGreeter>(
            scope => new LoggingGreeterDecorator(scope.GetService<ConsoleGreeter>())
        );

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IGreeter>();

        // Assert - decorator should be returned
        Assert.IsType<LoggingGreeterDecorator>(service);
        var decorator = (LoggingGreeterDecorator)service;
        Assert.IsType<ConsoleGreeter>(decorator.Inner);
    }

    // Helper decorator class for testing
    public class LoggingGreeterDecorator(IGreeter inner) : IGreeter
    {
        public IGreeter Inner { get; } = inner;

        public string Greet(string name)
        {
            // Log before
            var result = Inner.Greet(name);
            // Log after
            return result;
        }
    }

    #endregion

    #region Multi-Scope Tests

    [Fact]
    public void Integration_MultiScope_SingletonsShared_ScopedIsolated()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterSingleton<ILogger>(_ => new ConsoleLogger());
        container.RegisterScoped<IGreeter>(_ => new ConsoleGreeter());

        // Act
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        var logger1 = scope1.GetService<ILogger>();
        var logger2 = scope2.GetService<ILogger>();
        var greeter1 = scope1.GetService<IGreeter>();
        var greeter2 = scope2.GetService<IGreeter>();

        // Assert
        Assert.Same(logger1, logger2); // Singleton - same instance
        Assert.NotSame(greeter1, greeter2); // Scoped - different per scope
    }

    [Fact]
    public void Integration_ParallelScopes_WorkIndependently()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterScoped<ConsoleLogger>(_ => new ConsoleLogger());

        // Act
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        var logger1 = scope1.GetService<ConsoleLogger>();
        var logger2 = scope2.GetService<ConsoleLogger>();

        logger1.Log("Message from scope 1");
        logger2.Log("Message from scope 2");

        // Assert - each scope has isolated state
        Assert.Single(logger1.Messages);
        Assert.Single(logger2.Messages);
        Assert.Contains("scope 1", logger1.Messages[0]);
        Assert.Contains("scope 2", logger2.Messages[0]);
    }

    #endregion

    #region Build Optimization Tests

    [Fact]
    public void Integration_WithoutBuild_StillWorks()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());
        // Note: Build() is NOT called

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IGreeter>();

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Integration_WithBuild_PerformanceOptimized()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());
        container.Build(); // Optimize with FrozenDictionary

        using var scope = container.CreateScope();

        // Act - multiple resolutions
        var services = new IGreeter[1000];
        for (int i = 0; i < 1000; i++)
        {
            services[i] = scope.GetService<IGreeter>();
        }

        // Assert - all resolutions succeed
        Assert.All(services, s => Assert.NotNull(s));
    }

    #endregion
}
