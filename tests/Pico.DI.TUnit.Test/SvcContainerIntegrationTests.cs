namespace Pico.DI.TUnit.Test;

/// <summary>
/// Integration tests covering end-to-end scenarios.
/// </summary>
public class SvcContainerIntegrationTests : TUnitTestBase
{
    #region Test Services

    public class OrderService(IRepository<User> userRepository, ILogger logger)
    {
        public IRepository<User> UserRepository { get; } = userRepository;
        public ILogger Logger { get; } = logger;

        public void CreateOrder(int userId)
        {
            var user = UserRepository.GetById(userId);
            Logger.Log($"Creating order for user: {user?.Name ?? "Unknown"}");
        }
    }

    public class NotificationService(ILogger logger, IGreeter greeter)
    {
        public ILogger Logger { get; } = logger;
        public IGreeter Greeter { get; } = greeter;

        public string SendNotification(string userName)
        {
            var message = Greeter.Greet(userName);
            Logger.Log($"Sending notification: {message}");
            return message;
        }
    }

    #endregion

    #region Multi-Service Integration Tests

    [Test]
    public async Task Integration_MultipleServicesWithDependencies()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        RegisterConsoleLogger(container);
        container.RegisterTransient<IRepository<User>>(_ => new Repository<User>());
        container.RegisterTransient<OrderService>(
            scope =>
                new OrderService(scope.GetService<IRepository<User>>(), scope.GetService<ILogger>())
        );
        container.RegisterTransient<NotificationService>(
            scope =>
                new NotificationService(scope.GetService<ILogger>(), scope.GetService<IGreeter>())
        );

        using var scope = container.CreateScope();

        // Act
        var orderService = scope.GetService<OrderService>();
        var notificationService = scope.GetService<NotificationService>();

        // Assert
        await Assert.That(orderService).IsNotNull();
        await Assert.That(orderService.UserRepository).IsNotNull();
        await Assert.That(orderService.Logger).IsNotNull();
        await Assert.That(notificationService).IsNotNull();
        await Assert.That(notificationService.Logger).IsNotNull();
        await Assert.That(notificationService.Greeter).IsNotNull();
    }

    [Test]
    public async Task Integration_NotificationService_SendsCorrectMessage()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        RegisterConsoleLogger(container);
        container.RegisterTransient<NotificationService>(
            scope =>
                new NotificationService(scope.GetService<ILogger>(), scope.GetService<IGreeter>())
        );

        using var scope = container.CreateScope();
        var service = scope.GetService<NotificationService>();

        // Act
        var result = service.SendNotification("John");

        // Assert
        await Assert.That(result).IsEqualTo("Hello, John!");
    }

    #endregion

    #region Lifetime Integration Tests

    [Test]
    public async Task Integration_MixedLifetimes_WorkCorrectly()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterSingleton<ILogger>(_ => new ConsoleLogger());
        container.RegisterScoped<IGreeter>(_ => new ConsoleGreeter());
        container.RegisterTransient<NotificationService>(
            scope =>
                new NotificationService(scope.GetService<ILogger>(), scope.GetService<IGreeter>())
        );

        // Act - Get services from different scopes
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        var service1 = scope1.GetService<NotificationService>();
        var service2 = scope2.GetService<NotificationService>();

        // Assert - Singleton logger is shared
        await Assert.That(service1.Logger).IsSameReferenceAs(service2.Logger);
        // Scoped greeter is different per scope
        await Assert.That(service1.Greeter).IsNotSameReferenceAs(service2.Greeter);
        // Transient services are different
        await Assert.That(service1).IsNotSameReferenceAs(service2);
    }

    #endregion

    #region Scope Hierarchy Tests

    [Test]
    public async Task Integration_NestedScopes_ShareSingleton()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterSingleton<ILogger>(_ => new ConsoleLogger());
        container.RegisterScoped<IGreeter>(_ => new ConsoleGreeter());

        using var rootScope = container.CreateScope();
        using var childScope = rootScope.CreateScope();
        using var grandchildScope = childScope.CreateScope();

        // Act
        var rootLogger = rootScope.GetService<ILogger>();
        var childLogger = childScope.GetService<ILogger>();
        var grandchildLogger = grandchildScope.GetService<ILogger>();

        var rootGreeter = rootScope.GetService<IGreeter>();
        var childGreeter = childScope.GetService<IGreeter>();
        var grandchildGreeter = grandchildScope.GetService<IGreeter>();

        // Assert - Singletons shared across all scopes
        await Assert.That(rootLogger).IsSameReferenceAs(childLogger);
        await Assert.That(childLogger).IsSameReferenceAs(grandchildLogger);

        // Scoped instances are different per scope
        await Assert.That(rootGreeter).IsNotSameReferenceAs(childGreeter);
        await Assert.That(childGreeter).IsNotSameReferenceAs(grandchildGreeter);
    }

    #endregion

    #region Disposal Integration Tests

    [Test]
    public async Task Integration_ScopedDisposal_DisposesInCorrectOrder()
    {
        // Arrange
        using var container = new SvcContainer();
        var disposalOrder = new List<string>();

        container.RegisterScoped<DisposableService>(_ =>
        {
            var service = new DisposableService();
            return service;
        });

        DisposableService? innerService;
        DisposableService? outerService;

        using (var outerScope = container.CreateScope())
        {
            outerService = outerScope.GetService<DisposableService>();

            using (var innerScope = outerScope.CreateScope())
            {
                innerService = innerScope.GetService<DisposableService>();
                await Assert.That(innerService.IsDisposed).IsFalse();
            }

            // Inner scope disposed
            await Assert.That(innerService.IsDisposed).IsTrue();
            await Assert.That(outerService.IsDisposed).IsFalse();
        }

        // Outer scope disposed
        await Assert.That(outerService.IsDisposed).IsTrue();
    }

    #endregion

    #region Service Override Tests

    [Test]
    public async Task Integration_ServiceOverride_LastWins()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        container.RegisterTransient<IGreeter>(_ => new AlternativeGreeter()); // Override

        using var scope = container.CreateScope();

        // Act
        var greeter = scope.GetService<IGreeter>();
        var message = greeter.Greet("World");

        // Assert - Last registration wins
        await Assert.That(message).IsEqualTo("Hi, World!");
    }

    [Test]
    public async Task Integration_GetServices_ReturnsAllRegistrations()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container);
        RegisterAlternativeGreeter(container);

        using var scope = container.CreateScope();

        // Act
        var greeters = scope.GetServices<IGreeter>().ToList();
        var messages = greeters.Select(g => g.Greet("World")).ToList();

        // Assert
        await Assert.That(greeters.Count).IsEqualTo(2);
        await Assert.That(messages).Contains("Hello, World!");
        await Assert.That(messages).Contains("Hi, World!");
    }

    #endregion

    #region Build Integration Tests

    [Test]
    public async Task Integration_AfterBuild_PerformanceOptimized()
    {
        // Arrange
        using var container = new SvcContainer();
        RegisterConsoleGreeter(container, SvcLifetime.Singleton);
        RegisterConsoleLogger(container, SvcLifetime.Scoped);
        container.Build();

        // Act - Multiple resolutions should use optimized path
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            using var scope = container.CreateScope();
            var greeter = scope.GetService<IGreeter>();
            var logger = scope.GetService<ILogger>();
        }
        sw.Stop();

        // Assert - Should complete quickly (optimized FrozenDictionary)
        await Assert.That(sw.ElapsedMilliseconds).IsLessThan(1000);
    }

    #endregion
}
