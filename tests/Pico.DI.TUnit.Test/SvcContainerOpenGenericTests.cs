namespace Pico.DI.TUnit.Test;

/// <summary>
/// Tests for open generic type registration and resolution.
/// </summary>
public class SvcContainerOpenGenericTests : TUnitTestBase
{
    #region Open Generic Registration Tests

    [Test]
    public async Task RegisterTransient_OpenGeneric_ByType_Registers()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - registering open generic should not throw
        container.RegisterTransient(typeof(IRepository<>), typeof(Repository<>));

        // Assert - cannot directly resolve open generic, but registration should work
        await Assert.That(container).IsNotNull();
    }

    [Test]
    public async Task RegisterScoped_OpenGeneric_ByType_Registers()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act
        container.RegisterScoped(typeof(IRepository<>), typeof(Repository<>));

        // Assert
        await Assert.That(container).IsNotNull();
    }

    [Test]
    public async Task RegisterSingleton_OpenGeneric_ByType_Registers()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act
        container.RegisterSingleton(typeof(IRepository<>), typeof(Repository<>));

        // Assert
        await Assert.That(container).IsNotNull();
    }

    [Test]
    public async Task RegisterTransient_OpenGeneric_SelfType_Registers()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act
        container.RegisterTransient(typeof(Repository<>));

        // Assert
        await Assert.That(container).IsNotNull();
    }

    #endregion

    #region Closed Generic Factory Tests

    [Test]
    public async Task RegisterTransient_ClosedGeneric_ByFactory_ResolvesCorrectly()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<IRepository<User>>(_ => new Repository<User>());

        using var scope = container.CreateScope();

        // Act
        var repo = scope.GetService<IRepository<User>>();

        // Assert
        await Assert.That(repo).IsNotNull();
        await Assert.That(repo).IsTypeOf<Repository<User>>();
    }

    [Test]
    public async Task RegisterTransient_MultipleClosedGenerics_ResolvesCorrectly()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<IRepository<User>>(_ => new Repository<User>());
        container.RegisterTransient<IRepository<Product>>(_ => new Repository<Product>());

        using var scope = container.CreateScope();

        // Act
        var userRepo = scope.GetService<IRepository<User>>();
        var productRepo = scope.GetService<IRepository<Product>>();

        // Assert
        await Assert.That(userRepo).IsTypeOf<Repository<User>>();
        await Assert.That(productRepo).IsTypeOf<Repository<Product>>();
    }

    [Test]
    public async Task RegisterTransient_ClosedGeneric_CreatesNewInstanceEachTime()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<IRepository<User>>(_ => new Repository<User>());

        using var scope = container.CreateScope();

        // Act
        var repo1 = scope.GetService<IRepository<User>>();
        var repo2 = scope.GetService<IRepository<User>>();

        // Assert
        await Assert.That(repo1).IsNotSameReferenceAs(repo2);
    }

    [Test]
    public async Task RegisterSingleton_ClosedGeneric_ReturnsSameInstance()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterSingleton<IRepository<User>>(_ => new Repository<User>());

        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        // Act
        var repo1 = scope1.GetService<IRepository<User>>();
        var repo2 = scope2.GetService<IRepository<User>>();

        // Assert
        await Assert.That(repo1).IsSameReferenceAs(repo2);
    }

    [Test]
    public async Task RegisterScoped_ClosedGeneric_ScopeBehavior()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterScoped<IRepository<User>>(_ => new Repository<User>());

        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        // Act
        var repo1a = scope1.GetService<IRepository<User>>();
        var repo1b = scope1.GetService<IRepository<User>>();
        var repo2 = scope2.GetService<IRepository<User>>();

        // Assert
        await Assert.That(repo1a).IsSameReferenceAs(repo1b);
        await Assert.That(repo1a).IsNotSameReferenceAs(repo2);
    }

    #endregion

    #region Multiple Type Parameter Generic Tests

    [Test]
    public async Task Register_MultiTypeParameterGeneric_ResolvesCorrectly()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<ICache<string, User>>(_ => new MemoryCache<string, User>());

        using var scope = container.CreateScope();

        // Act
        var cache = scope.GetService<ICache<string, User>>();

        // Assert
        await Assert.That(cache).IsNotNull();
        await Assert.That(cache).IsTypeOf<MemoryCache<string, User>>();
    }

    [Test]
    public async Task Register_MultiTypeParameterGeneric_MultipleCombinations()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<ICache<string, User>>(_ => new MemoryCache<string, User>());
        container.RegisterTransient<ICache<int, Product>>(_ => new MemoryCache<int, Product>());

        using var scope = container.CreateScope();

        // Act
        var userCache = scope.GetService<ICache<string, User>>();
        var productCache = scope.GetService<ICache<int, Product>>();

        // Assert
        await Assert.That(userCache).IsTypeOf<MemoryCache<string, User>>();
        await Assert.That(productCache).IsTypeOf<MemoryCache<int, Product>>();
    }

    #endregion

    #region Generic Service Functionality Tests

    [Test]
    public async Task Repository_CanStoreAndRetrieveEntities()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterSingleton<IRepository<User>>(_ => new Repository<User>());

        using var scope = container.CreateScope();
        var repo = scope.GetService<IRepository<User>>();

        // Act
        var user = new User { Name = "John" };
        repo.Add(user);
        var retrieved = repo.GetById(1);

        // Assert
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Name).IsEqualTo("John");
    }

    [Test]
    public async Task Cache_CanSetAndGetValues()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterSingleton<ICache<string, User>>(_ => new MemoryCache<string, User>());

        using var scope = container.CreateScope();
        var cache = scope.GetService<ICache<string, User>>();

        // Act
        var user = new User { Name = "Jane" };
        cache.Set("user1", user);
        var retrieved = cache.Get("user1");

        // Assert
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Name).IsEqualTo("Jane");
    }

    #endregion
}
