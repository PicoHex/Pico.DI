namespace Pico.DI.Sample;

using Pico.DI.Abs;
using Pico.DI.Gen;

/// <summary>
/// Demonstrates decorator generic pattern with AOT-compatible dependency injection.
///
/// This example shows how to:
/// 1. Register a service (e.g., IUser -> User)
/// 2. Register a decorator generic (e.g., Logger<T>)
/// 3. Automatically inject Logger<IUser> that wraps the IUser service
///
/// All code generation happens at compile-time. Zero runtime reflection.
/// Fully compatible with Native AOT and IL trimming.
/// </summary>
public class DecoratorGenericSample
{
    #region Service and Decorator Types

    /// <summary>
    /// Sample service interface
    /// </summary>
    public interface IUserService
    {
        string GetUserName();
    }

    public class UserService : IUserService
    {
        public string GetUserName() => "Alice";
    }

    public interface IDatabaseService
    {
        void Execute(string query);
    }

    public class DatabaseService : IDatabaseService
    {
        public void Execute(string query)
        {
            Console.WriteLine($"Executing: {query}");
        }
    }

    /// <summary>
    /// Generic decorator that logs all accesses to a service.
    /// Can wrap any service type T.
    ///
    /// With the source generator detecting GetService<Logger<T>> calls,
    /// it automatically generates closed generic decorators at compile time.
    /// </summary>
    public class Logger<T>
        where T : class
    {
        private readonly T _innerService;
        private readonly List<string> _accessLog = [];

        /// <summary>
        /// Constructor: receives the service to be decorated.
        /// Source generator recognizes this parameter as the wrapped service.
        /// </summary>
        public Logger(T innerService)
        {
            _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
            Console.WriteLine($"[Logger<{typeof(T).Name}>] Initialized");
        }

        /// <summary>
        /// Provides access to the wrapped service.
        /// In a real scenario, you might use Reflection.Emit or dynamic proxies,
        /// but this example shows the explicit approach compatible with AOT.
        /// </summary>
        public T Service => _innerService;

        public IReadOnlyList<string> GetAccessLog() => _accessLog;

        public void LogAccess(string operation)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] {typeof(T).Name}.{operation}";
            _accessLog.Add(logEntry);
            Console.WriteLine(logEntry);
        }
    }

    /// <summary>
    /// More complex decorator with multiple dependencies.
    /// </summary>
    public class CachingDecorator<T>
        where T : class
    {
        private readonly T _service;
        private readonly IMetricsCollector _metrics;

        public CachingDecorator(T service, IMetricsCollector metrics)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }

        public T Service => _service;

        public void RecordCacheHit()
        {
            _metrics.RecordEvent("cache_hit");
        }
    }

    public interface IMetricsCollector
    {
        void RecordEvent(string eventName);
    }

    public class ConsoleMetricsCollector : IMetricsCollector
    {
        public void RecordEvent(string eventName)
        {
            Console.WriteLine($"[Metrics] Event: {eventName}");
        }
    }

    #endregion

    #region Main Demo

    public static void Main(string[] args)
    {
        Console.WriteLine("=== Decorator Generic Pattern with AOT ===\n");

        using var container = new SvcContainer();

        // Step 1: Register services
        Console.WriteLine("Step 1: Registering services");
        container
            .RegisterSingleton<IUserService, UserService>()
            .RegisterSingleton<IDatabaseService, DatabaseService>()
            .RegisterSingleton<IMetricsCollector, ConsoleMetricsCollector>();

        // Step 2: Register decorator generics
        // This tells the source generator that Logger<T> can wrap any service
        Console.WriteLine("Step 2: Registering decorator generics");
        container
            .RegisterDecorator<Logger<>>(SvcLifetime.Transient)
            .RegisterDecorator<CachingDecorator<>>(SvcLifetime.Transient);

        // Note: In a real AOT scenario, the source generator would:
        // 1. Detect RegisterDecorator<Logger<>>() call
        // 2. Scan all GetService<Logger<T>> calls in the code
        // 3. Pre-generate factories for Logger<IUserService>, Logger<IDatabaseService>, etc.

        // Step 3: Configure generated services
        // The source generator produces a ConfigureGeneratedServices() extension method
        Console.WriteLine("Step 3: Configuring generated services");
        container.ConfigureGeneratedServices();

        // Step 4: Resolve and use services with decorators
        Console.WriteLine("\nStep 4: Resolving services with decorators\n");

        using var scope = container.CreateScope();

        // Example 1: Logger<IUserService>
        Console.WriteLine("--- Logger<IUserService> ---");
        var userServiceLogger = scope.GetService<Logger<IUserService>>();
        userServiceLogger.LogAccess("GetUserName");
        var userName = userServiceLogger.Service.GetUserName();
        Console.WriteLine($"User: {userName}");

        // Example 2: Logger<IDatabaseService>
        Console.WriteLine("\n--- Logger<IDatabaseService> ---");
        var dbServiceLogger = scope.GetService<Logger<IDatabaseService>>();
        dbServiceLogger.LogAccess("Execute");
        dbServiceLogger.Service.Execute("SELECT * FROM Users");

        // Example 3: Decorator with multiple dependencies
        Console.WriteLine("\n--- CachingDecorator<IDatabaseService> ---");
        var cachingDb = scope.GetService<CachingDecorator<IDatabaseService>>();
        cachingDb.RecordCacheHit();

        Console.WriteLine("\n=== Demo Complete ===");
    }

    #endregion
}

/// <summary>
/// This class demonstrates what the source generator would generate
/// for decorator support (pseudo-code for documentation).
/// </summary>
internal static class GeneratedDecoratorFactories
{
    /*
     * When the source generator detects:
     * 1. container.RegisterDecorator<Logger<>>(SvcLifetime.Transient);
     * 2. var logger = scope.GetService<Logger<IUserService>>();
     *
     * It generates code equivalent to:
     *
     * container.RegisterTransient<Logger<IUserService>>(
     *     scope => new Logger<IUserService>(scope.GetService<IUserService>())
     * );
     *
     * container.RegisterTransient<Logger<IDatabaseService>>(
     *     scope => new Logger<IDatabaseService>(scope.GetService<IDatabaseService>())
     * );
     *
     * And similar for CachingDecorator<T> with its additional dependencies.
     *
     * All of this is pre-compiled at build time, with zero runtime reflection.
     * This makes it fully AOT-compatible.
     */
}
