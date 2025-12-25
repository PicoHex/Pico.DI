namespace Pico.IoC.Test;

/// <summary>
/// Tests for open generic type registration and resolution.
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

    [Fact]
    public void RegisterOpenGeneric_Transient_ResolvesClosedType()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterOpenGenericTransient(typeof(IRepository<>), typeof(Repository<>));

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
        // Arrange
        using var container = new SvcContainer();
        container.RegisterOpenGenericTransient(typeof(IRepository<>), typeof(Repository<>));

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
        // Arrange
        using var container = new SvcContainer();
        container.RegisterOpenGenericScoped(typeof(IRepository<>), typeof(Repository<>));

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
        // Arrange
        using var container = new SvcContainer();
        container.RegisterOpenGenericScoped(typeof(IRepository<>), typeof(Repository<>));

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
        // Arrange
        using var container = new SvcContainer();
        container.RegisterOpenGenericSingleton(typeof(IRepository<>), typeof(Repository<>));

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
        // Arrange
        using var container = new SvcContainer();
        container.RegisterOpenGenericScoped(typeof(ICache<,>), typeof(MemoryCache<,>));

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
        // Arrange
        using var container = new SvcContainer();
        container.RegisterOpenGenericScoped(typeof(IRepository<>), typeof(Repository<>));
        container.RegisterTransient<UserService>(scope => new UserService(
            scope.GetService<IRepository<User>>()
        ));

        using var scope = container.CreateScope();

        // Act
        var userService = scope.GetService<UserService>();

        // Assert
        Assert.NotNull(userService);
        Assert.NotNull(userService.UserRepository);
        Assert.IsType<Repository<User>>(userService.UserRepository);
    }

    [Fact]
    public void RegisterOpenGeneric_ThrowsForNonOpenGenericServiceType()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () =>
                container.RegisterOpenGeneric(
                    typeof(IRepository<User>), // Not open generic
                    typeof(Repository<>),
                    SvcLifetime.Transient
                )
        );
    }

    [Fact]
    public void RegisterOpenGeneric_ThrowsForNonOpenGenericImplementationType()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () =>
                container.RegisterOpenGeneric(
                    typeof(IRepository<>),
                    typeof(Repository<User>), // Not open generic
                    SvcLifetime.Transient
                )
        );
    }

    [Fact]
    public void RegisterOpenGeneric_ClosedTypeOverridesOpenGeneric()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterOpenGenericScoped(typeof(IRepository<>), typeof(Repository<>));
        // Register a specific closed type that should take precedence
        container.RegisterScoped<IRepository<User>>(scope => new SpecialUserRepository());

        using var scope = container.CreateScope();

        // Act
        var userRepo = scope.GetService<IRepository<User>>();
        var productRepo = scope.GetService<IRepository<Product>>();

        // Assert
        Assert.IsType<SpecialUserRepository>(userRepo);
        Assert.IsType<Repository<Product>>(productRepo);
    }

    private class SpecialUserRepository : IRepository<User>
    {
        public User? GetById(int id) => new User { Id = id, Name = "Special" };

        public void Add(User entity) { }
    }
}
