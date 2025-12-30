namespace Pico.DI.Test;

/// <summary>
/// Tests for open generic type registration and resolution.
/// NOTE: For AOT compatibility, open generic registrations require the source generator
/// to detect GetService&lt;T&gt; calls and pre-generate closed type factories.
/// In unit tests without the generator active, we use manual factory registration.
/// </summary>
public class SvcContainerOpenGenericTests : SvcContainerTestBase
{
    #region Test Services

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

    public class UserService(IRepository<User> userRepository)
    {
        public IRepository<User> UserRepository { get; } = userRepository;
    }

    #endregion

    #region AOT-Compatible Tests Using Factory Registration

    [Fact]
    public void RegisterOpenGeneric_Transient_ResolvesClosedType()
    {
        // Arrange - For AOT, use explicit factory registration
        using var container = new SvcContainer();
        container.RegisterTransient<IRepository<User>>(scope => new Repository<User>());
        container.RegisterTransient<IRepository<Product>>(scope => new Repository<Product>());

        using var scope = container.CreateScope();

        // Act
        var userRepo = scope.GetService<IRepository<User>>();
        var productRepo = scope.GetService<IRepository<Product>>();

        // Assert
        Assert.NotNull(userRepo);
        Assert.NotNull(productRepo);
        Assert.IsType<Repository<User>>(userRepo);
        Assert.IsType<Repository<Product>>(productRepo);
    }

    [Fact]
    public void RegisterOpenGeneric_Transient_CreatesNewInstanceEachTime()
    {
        // Arrange - For AOT, use explicit factory registration
        using var container = new SvcContainer();
        container.RegisterTransient<IRepository<User>>(scope => new Repository<User>());

        using var scope = container.CreateScope();

        // Act
        var repo1 = scope.GetService<IRepository<User>>();
        var repo2 = scope.GetService<IRepository<User>>();

        // Assert
        Assert.NotSame(repo1, repo2);
    }

    [Fact]
    public void RegisterOpenGeneric_Scoped_SharesInstanceWithinScope()
    {
        // Arrange - For AOT, use explicit factory registration
        using var container = new SvcContainer();
        container.RegisterScoped<IRepository<User>>(scope => new Repository<User>());

        using var scope = container.CreateScope();

        // Act
        var repo1 = scope.GetService<IRepository<User>>();
        var repo2 = scope.GetService<IRepository<User>>();

        // Assert
        Assert.Same(repo1, repo2);
    }

    [Fact]
    public void RegisterOpenGeneric_Scoped_DifferentInstancesAcrossScopes()
    {
        // Arrange - For AOT, use explicit factory registration
        using var container = new SvcContainer();
        container.RegisterScoped<IRepository<User>>(scope => new Repository<User>());

        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        // Act
        var repo1 = scope1.GetService<IRepository<User>>();
        var repo2 = scope2.GetService<IRepository<User>>();

        // Assert
        Assert.NotSame(repo1, repo2);
    }

    [Fact]
    public void RegisterOpenGeneric_Singleton_SharesInstanceGlobally()
    {
        // Arrange - For AOT, use explicit factory registration
        using var container = new SvcContainer();
        container.RegisterSingleton<IRepository<User>>(scope => new Repository<User>());

        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        // Act
        var repo1 = scope1.GetService<IRepository<User>>();
        var repo2 = scope2.GetService<IRepository<User>>();

        // Assert
        Assert.Same(repo1, repo2);
    }

    [Fact]
    public void RegisterOpenGeneric_WithMultipleTypeParameters_Works()
    {
        // Arrange - For AOT, use explicit factory registration
        using var container = new SvcContainer();
        container.RegisterScoped<ICache<string, User>>(scope => new MemoryCache<string, User>());

        using var scope = container.CreateScope();

        // Act
        var cache = scope.GetService<ICache<string, User>>();

        // Assert
        Assert.NotNull(cache);
        Assert.IsType<MemoryCache<string, User>>(cache);

        // Verify it works
        var user = new User { Id = 1, Name = "Test" };
        cache.Set("user1", user);
        var retrieved = cache.Get("user1");
        Assert.Same(user, retrieved);
    }

    [Fact]
    public void RegisterOpenGeneric_CanBeInjectedIntoDependentService()
    {
        // Arrange - For AOT, use explicit factory registration
        using var container = new SvcContainer();
        container.RegisterScoped<IRepository<User>>(scope => new Repository<User>());
        container.RegisterTransient<UserService>(
            scope => new UserService(scope.GetService<IRepository<User>>())
        );

        using var scope = container.CreateScope();

        // Act
        var userService = scope.GetService<UserService>();

        // Assert
        Assert.NotNull(userService);
        Assert.NotNull(userService.UserRepository);
        Assert.IsType<Repository<User>>(userService.UserRepository);
    }

    #endregion

    #region Open Generic API Validation Tests

    [Fact]
    public void Register_OpenGeneric_NonOpenGenericServiceType_IsNoOp()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act & Assert - Non-open-generic registration now requires generated registrations
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () =>
                container.Register(
                    typeof(IRepository<User>), // Not open generic - handled by source generator
                    typeof(Repository<User>),
                    SvcLifetime.Transient
                )
        );
    }

    [Fact]
    public void Register_ClosedTypeOverridesOpenGeneric()
    {
        // Arrange - For AOT, manually register both types
        using var container = new SvcContainer();
        // Base registration
        container.RegisterScoped<IRepository<Product>>(scope => new Repository<Product>());
        // Specific override for User
        container.RegisterScoped<IRepository<User>>(scope => new SpecialUserRepository());

        using var scope = container.CreateScope();

        // Act
        var userRepo = scope.GetService<IRepository<User>>();
        var productRepo = scope.GetService<IRepository<Product>>();

        // Assert
        Assert.IsType<SpecialUserRepository>(userRepo);
        Assert.IsType<Repository<Product>>(productRepo);
    }

    #endregion

    #region Open Generic Registration Stores Descriptor (for Source Generator to Use)

    [Fact]
    public void Register_OpenGeneric_StoresDescriptorForSourceGenerator()
    {
        // This test verifies that Register with open generic types stores the descriptor
        // The source generator will use this to generate closed type factories
        using var container = new SvcContainer();

        // Act - This stores the open generic descriptor using the unified Register API
        container.RegisterScoped(typeof(IRepository<>), typeof(Repository<>));

        // The container now has the open generic descriptor stored
        // In a real AOT scenario, the source generator would detect GetService<IRepository<User>>()
        // calls and generate the closed type registrations at compile time

        // For now, verify that requesting an unregistered closed type gives a helpful error
        using var scope = container.CreateScope();
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IRepository<User>>());
        Assert.Contains("was not detected at compile time", ex.Message);
    }

    [Fact]
    public void Register_OpenGeneric_WithLifetime_StoresDescriptor()
    {
        // Test the generic Register method with lifetime parameter
        using var container = new SvcContainer();

        // Act - Use the unified Register API with explicit lifetime
        container.Register(typeof(IRepository<>), typeof(Repository<>), SvcLifetime.Transient);

        // Verify the descriptor is stored
        using var scope = container.CreateScope();
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IRepository<User>>());
        Assert.Contains("was not detected at compile time", ex.Message);
    }

    #endregion

    [Fact]
    public void OpenGeneric_Registration_AutoGenerates_ClosedFromCtorDependency()
    {
        // Arrange
        using var container = new SvcContainer();

        // Register open generic mapping ILog<> -> Logger<>
        container.Register(typeof(ILog<>), typeof(Logger<>), SvcLifetime.Transient);

        // Register a concrete type that depends on ILog<Concrete>
        container.Register<ServiceWithLog>(SvcLifetime.Transient);

        // Act
        using var scope = container.CreateScope();
        var svc = scope.GetService<ServiceWithLog>();

        // Assert
        Assert.NotNull(svc);
        Assert.NotNull(svc.Logger);
        Assert.IsType<Logger<ServiceWithLog>>(svc.Logger);
    }

    // Helper types for the test
    public interface ILog<T> { }

    public class Logger<T> : ILog<T>
    {
        public T Inner { get; }

        public Logger(T inner)
        {
            Inner = inner;
        }
    }

    public class ServiceWithLog
    {
        public ILog<ServiceWithLog> Logger { get; }

        public ServiceWithLog(ILog<ServiceWithLog> logger)
        {
            Logger = logger;
        }
    }

    private class SpecialUserRepository : IRepository<User>
    {
        public User? GetById(int id) => new User { Id = id, Name = "Special" };

        public void Add(User entity) { }
    }
}
