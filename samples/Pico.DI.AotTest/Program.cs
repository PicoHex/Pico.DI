// AOT Test Program - Tests DI container with TrimMode=full
// This project uses net9.0 to verify AOT compatibility

namespace Pico.DI.AotTest;

#region Abstractions

public enum SvcLifetime
{
    Transient,
    Scoped,
    Singleton
}

public interface ISvcScope : IDisposable, IAsyncDisposable
{
    object GetService(Type serviceType);
    TService GetService<TService>()
        where TService : class => (TService)GetService(typeof(TService));
    ISvcScope CreateScope();
}

public interface ISvcContainer : IDisposable, IAsyncDisposable
{
    ISvcContainer Register(SvcDescriptor descriptor);
    ISvcScope CreateScope();
}

public class SvcDescriptor(
    Type serviceType,
    Type? implementationType,
    SvcLifetime lifetime = SvcLifetime.Singleton
)
{
    public Type ServiceType { get; } =
        serviceType ?? throw new ArgumentNullException(nameof(serviceType));
    public Type ImplementationType { get; } = implementationType ?? serviceType;
    public object? SingleInstance { get; set; }
    public Func<ISvcScope, object>? Factory { get; }
    public SvcLifetime Lifetime { get; } = lifetime;

    public SvcDescriptor(Type serviceType, object instance)
        : this(serviceType, serviceType) =>
        SingleInstance = instance ?? throw new ArgumentNullException(nameof(instance));

    public SvcDescriptor(
        Type serviceType,
        Func<ISvcScope, object> factory,
        SvcLifetime lifetime = SvcLifetime.Singleton
    )
        : this(serviceType, serviceType, lifetime) =>
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
}

public class PicoDiException(string message) : Exception(message);

#endregion

#region Container Implementation

public class SvcContainer : ISvcContainer
{
    private readonly ConcurrentDictionary<Type, List<SvcDescriptor>> _descriptorCache = new();
    private bool _disposed;

    public ISvcContainer Register(SvcDescriptor descriptor)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _descriptorCache.AddOrUpdate(
            descriptor.ServiceType,
            _ => [descriptor],
            (_, list) =>
            {
                list.Add(descriptor);
                return list;
            }
        );
        return this;
    }

    public ISvcScope CreateScope()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new SvcScope(_descriptorCache);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        foreach (
            var svc in _descriptorCache
                .SelectMany(p => p.Value)
                .Select(p => p.SingleInstance)
                .Where(p => p is not null)
        )
        {
            if (svc is IDisposable disposable)
                disposable.Dispose();
        }
        _descriptorCache.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        foreach (
            var svc in _descriptorCache
                .SelectMany(p => p.Value)
                .Select(p => p.SingleInstance)
                .Where(p => p is not null)
        )
        {
            switch (svc)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync();
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
        _descriptorCache.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

public sealed class SvcScope(ConcurrentDictionary<Type, List<SvcDescriptor>> descriptorCache)
    : ISvcScope
{
    private readonly ConcurrentDictionary<SvcDescriptor, object> _scopedInstances = new();
    private readonly ConcurrentDictionary<SvcDescriptor, object> _singletonLocks = new();
    private bool _disposed;

    public ISvcScope CreateScope()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new SvcScope(descriptorCache);
    }

    public object GetService(Type serviceType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!descriptorCache.TryGetValue(serviceType, out var resolvers))
            throw new PicoDiException($"Service type '{serviceType.FullName}' is not registered.");

        var resolver =
            resolvers.LastOrDefault()
            ?? throw new PicoDiException(
                $"No service descriptor found for type '{serviceType.FullName}'."
            );

        return resolver.Lifetime switch
        {
            SvcLifetime.Transient
                => resolver.Factory != null
                    ? resolver.Factory(this)
                    : throw new PicoDiException(
                        $"No factory registered for transient service '{serviceType.FullName}'."
                    ),
            SvcLifetime.Singleton => GetOrCreateSingleton(serviceType, resolver),
            SvcLifetime.Scoped => GetOrAddScopedInstance(resolver),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private object GetOrCreateSingleton(Type serviceType, SvcDescriptor resolver)
    {
        if (resolver.SingleInstance != null)
            return resolver.SingleInstance;

        var singletonLock = _singletonLocks.GetOrAdd(resolver, _ => new object());
        lock (singletonLock)
        {
            if (resolver.SingleInstance != null)
                return resolver.SingleInstance;
            resolver.SingleInstance =
                resolver.Factory != null
                    ? resolver.Factory(this)
                    : throw new PicoDiException(
                        $"No factory or instance registered for singleton service '{serviceType.FullName}'."
                    );
            return resolver.SingleInstance;
        }
    }

    private object GetOrAddScopedInstance(SvcDescriptor resolver) =>
        _scopedInstances.GetOrAdd(
            resolver,
            desc =>
                desc.Factory != null
                    ? desc.Factory(this)
                    : throw new PicoDiException(
                        $"No factory registered for scoped service '{desc.ServiceType.FullName}'."
                    )
        );

    public void Dispose()
    {
        if (_disposed)
            return;
        foreach (var svc in _scopedInstances.Values)
        {
            if (svc is IDisposable disposable)
                disposable.Dispose();
        }
        _scopedInstances.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        foreach (var svc in _scopedInstances.Values)
        {
            switch (svc)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync();
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
        _scopedInstances.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

#endregion

#region Extension Methods (without C# 14 extensions)

public static class SvcContainerExtensions
{
    public static ISvcContainer Register<TService>(
        this ISvcContainer container,
        Func<ISvcScope, TService> factory,
        SvcLifetime lifetime
    )
        where TService : class =>
        container.Register(new SvcDescriptor(typeof(TService), factory, lifetime));

    public static ISvcContainer RegisterTransient<TService>(
        this ISvcContainer container,
        Func<ISvcScope, TService> factory
    )
        where TService : class =>
        container.Register(new SvcDescriptor(typeof(TService), factory, SvcLifetime.Transient));

    public static ISvcContainer RegisterScoped<TService>(
        this ISvcContainer container,
        Func<ISvcScope, TService> factory
    )
        where TService : class =>
        container.Register(new SvcDescriptor(typeof(TService), factory, SvcLifetime.Scoped));

    public static ISvcContainer RegisterSingleton<TService>(
        this ISvcContainer container,
        Func<ISvcScope, TService> factory
    )
        where TService : class =>
        container.Register(new SvcDescriptor(typeof(TService), factory, SvcLifetime.Singleton));

    public static ISvcContainer RegisterSingle<TService>(
        this ISvcContainer container,
        TService instance
    )
        where TService : class => container.Register(new SvcDescriptor(typeof(TService), instance));
}

#endregion

#region Test Services

public interface ILogger
{
    void Log(string message);
}

public class ConsoleLogger : ILogger
{
    public void Log(string message) => Console.WriteLine($"[LOG] {message}");
}

public interface IGreeter
{
    string Greet(string name);
}

public class Greeter : IGreeter
{
    public string Greet(string name) => $"Hello, {name}!";
}

public class GreetingService(IGreeter greeter, ILogger logger)
{
    public void SayHello(string name)
    {
        logger.Log($"Greeting {name}");
        Console.WriteLine(greeter.Greet(name));
    }
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

    public void Add(T entity) => _store[_nextId++] = entity;
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

#endregion

#region Main Program

public static class Program
{
    public static void Main()
    {
        Console.WriteLine("=== Pico.DI AOT Test (TrimMode=full) ===\n");

        using var container = new SvcContainer();

        // Register services with AOT-compatible factory delegates
        container
            .RegisterSingleton<ILogger>(_ => new ConsoleLogger())
            .RegisterTransient<IGreeter>(_ => new Greeter())
            .RegisterScoped<GreetingService>(scope => new GreetingService(
                scope.GetService<IGreeter>(),
                scope.GetService<ILogger>()
            ))
            // Open generic simulation - pre-register closed types
            .RegisterScoped<IRepository<User>>(_ => new Repository<User>());

        // Test 1: Basic resolution
        Console.WriteLine("Test 1: Basic Service Resolution");
        using (var scope = container.CreateScope())
        {
            var greetingService = scope.GetService<GreetingService>();
            greetingService.SayHello("AOT World");
        }
        Console.WriteLine("✓ Test 1 passed\n");

        // Test 2: Singleton lifetime
        Console.WriteLine("Test 2: Singleton Lifetime");
        using (var scope1 = container.CreateScope())
        using (var scope2 = container.CreateScope())
        {
            var logger1 = scope1.GetService<ILogger>();
            var logger2 = scope2.GetService<ILogger>();
            Console.WriteLine($"Same instance: {ReferenceEquals(logger1, logger2)}");
            if (!ReferenceEquals(logger1, logger2))
                throw new Exception("Singleton test failed!");
        }
        Console.WriteLine("✓ Test 2 passed\n");

        // Test 3: Transient lifetime
        Console.WriteLine("Test 3: Transient Lifetime");
        using (var scope = container.CreateScope())
        {
            var greeter1 = scope.GetService<IGreeter>();
            var greeter2 = scope.GetService<IGreeter>();
            Console.WriteLine($"Different instances: {!ReferenceEquals(greeter1, greeter2)}");
            if (ReferenceEquals(greeter1, greeter2))
                throw new Exception("Transient test failed!");
        }
        Console.WriteLine("✓ Test 3 passed\n");

        // Test 4: Scoped lifetime
        Console.WriteLine("Test 4: Scoped Lifetime");
        using (var scope1 = container.CreateScope())
        using (var scope2 = container.CreateScope())
        {
            var service1a = scope1.GetService<GreetingService>();
            var service1b = scope1.GetService<GreetingService>();
            var service2 = scope2.GetService<GreetingService>();
            Console.WriteLine($"Same within scope: {ReferenceEquals(service1a, service1b)}");
            Console.WriteLine($"Different across scopes: {!ReferenceEquals(service1a, service2)}");
            if (!ReferenceEquals(service1a, service1b) || ReferenceEquals(service1a, service2))
                throw new Exception("Scoped test failed!");
        }
        Console.WriteLine("✓ Test 4 passed\n");

        // Test 5: Generic repository
        Console.WriteLine("Test 5: Generic Repository");
        using (var scope = container.CreateScope())
        {
            var userRepo = scope.GetService<IRepository<User>>();
            userRepo.Add(new User { Id = 1, Name = "Test User" });
            var user = userRepo.GetById(1);
            Console.WriteLine($"User retrieved: {user?.Name}");
            if (user?.Name != "Test User")
                throw new Exception("Repository test failed!");
        }
        Console.WriteLine("✓ Test 5 passed\n");

        Console.WriteLine("=== All AOT Tests Passed! ===");
    }
}

#endregion
