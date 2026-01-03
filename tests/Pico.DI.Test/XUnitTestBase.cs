namespace Pico.DI.Test;

/// <summary>
/// Test base class containing shared test services and utilities for xUnit tests.
/// </summary>
public abstract class XUnitTestBase
{
    #region Test Services

    public interface IGreeter
    {
        string Greet(string name);
    }

    public class ConsoleGreeter : IGreeter
    {
        public string Greet(string name) => $"Hello, {name}!";
    }

    public class AlternativeGreeter : IGreeter
    {
        public string Greet(string name) => $"Hi, {name}!";
    }

    public interface ILogger
    {
        void Log(string message);
    }

    public class ConsoleLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public void Log(string message) => Messages.Add($"[LOG] {message}");
    }

    public interface IRepository<T>
    {
        T? GetById(int id);
        void Add(T entity);
    }

    public class Repository<T> : IRepository<T>
    {
        private readonly Dictionary<int, T> _store = new();
        private int _nextId = 1;

        public T? GetById(int id) => _store.TryGetValue(id, out var entity) ? entity : default;

        public void Add(T entity)
        {
            _store[_nextId++] = entity;
        }
    }

    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class Product
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public interface ICache<TKey, TValue>
    {
        TValue? Get(TKey key);
        void Set(TKey key, TValue value);
    }

    public class MemoryCache<TKey, TValue> : ICache<TKey, TValue>
        where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> _cache = new();

        public TValue? Get(TKey key) => _cache.TryGetValue(key, out var value) ? value : default;

        public void Set(TKey key, TValue value) => _cache[key] = value;
    }

    public class ServiceWithDependency(IGreeter greeter)
    {
        public IGreeter Greeter { get; } = greeter;
    }

    public class ServiceWithMultipleDependencies(IGreeter greeter, ILogger logger)
    {
        public IGreeter Greeter { get; } = greeter;
        public ILogger Logger { get; } = logger;
    }

    public class DisposableService : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    public class AsyncDisposableService : IAsyncDisposable
    {
        public bool IsDisposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    public class BothDisposableService : IDisposable, IAsyncDisposable
    {
        public bool IsSyncDisposed { get; private set; }
        public bool IsAsyncDisposed { get; private set; }

        public void Dispose() => IsSyncDisposed = true;

        public ValueTask DisposeAsync()
        {
            IsAsyncDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Service that tracks creation count for testing singleton behavior
    /// </summary>
    public class CountingService
    {
        private static int _instanceCount;
        public int InstanceId { get; }

        public CountingService()
        {
            InstanceId = Interlocked.Increment(ref _instanceCount);
        }

        public static void ResetCounter() => _instanceCount = 0;

        public static int GetInstanceCount() => _instanceCount;
    }

    #endregion

    #region Test Helpers

    protected static void RegisterConsoleGreeter(
        ISvcContainer container,
        SvcLifetime lifetime = SvcLifetime.Transient
    )
    {
        switch (lifetime)
        {
            case SvcLifetime.Singleton:
                container.RegisterSingleton<IGreeter>(_ => new ConsoleGreeter());
                break;
            case SvcLifetime.Scoped:
                container.RegisterScoped<IGreeter>(_ => new ConsoleGreeter());
                break;
            default:
                container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());
                break;
        }
    }

    protected static void RegisterAlternativeGreeter(
        ISvcContainer container,
        SvcLifetime lifetime = SvcLifetime.Transient
    )
    {
        switch (lifetime)
        {
            case SvcLifetime.Singleton:
                container.RegisterSingleton<IGreeter>(_ => new AlternativeGreeter());
                break;
            case SvcLifetime.Scoped:
                container.RegisterScoped<IGreeter>(_ => new AlternativeGreeter());
                break;
            default:
                container.RegisterTransient<IGreeter>(_ => new AlternativeGreeter());
                break;
        }
    }

    protected static void RegisterConsoleLogger(
        ISvcContainer container,
        SvcLifetime lifetime = SvcLifetime.Transient
    )
    {
        switch (lifetime)
        {
            case SvcLifetime.Singleton:
                container.RegisterSingleton<ILogger>(_ => new ConsoleLogger());
                break;
            case SvcLifetime.Scoped:
                container.RegisterScoped<ILogger>(_ => new ConsoleLogger());
                break;
            default:
                container.RegisterTransient<ILogger>(_ => new ConsoleLogger());
                break;
        }
    }

    #endregion
}
