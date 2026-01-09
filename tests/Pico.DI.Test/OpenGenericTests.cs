namespace Pico.DI.Test;

/// <summary>
/// Tests for open generic registrations with all lifetimes.
/// Open generics: IRepository&lt;&gt; -&gt; Repository&lt;&gt;
/// </summary>
public class OpenGenericTests
{
    #region Transient Open Generic

    [Test]
    public async Task OpenGeneric_Transient_ReturnsNewInstanceEachTime()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient(typeof(IRepository<>), typeof(Repository<>));
        
        // Register factory for closed generic (simulating source generator output)
        container.RegisterTransient<IRepository<User>>(static _ => new Repository<User>());
        using var scope = container.CreateScope();

        // Act
        var repo1 = scope.GetService<IRepository<User>>();
        var repo2 = scope.GetService<IRepository<User>>();

        // Assert
        await Assert.That(repo1).IsNotNull();
        await Assert.That(repo2).IsNotNull();
        await Assert.That(repo1.InstanceId).IsNotEqualTo(repo2.InstanceId);
        await Assert.That(repo1.EntityType).IsEqualTo(typeof(User));
    }

    [Test]
    public async Task OpenGeneric_Transient_DifferentTypeArguments_DifferentInstances()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<IRepository<User>>(static _ => new Repository<User>());
        container.RegisterTransient<IRepository<Order>>(static _ => new Repository<Order>());
        using var scope = container.CreateScope();

        // Act
        var userRepo = scope.GetService<IRepository<User>>();
        var orderRepo = scope.GetService<IRepository<Order>>();

        // Assert
        await Assert.That(userRepo.EntityType).IsEqualTo(typeof(User));
        await Assert.That(orderRepo.EntityType).IsEqualTo(typeof(Order));
        await Assert.That(userRepo.InstanceId).IsNotEqualTo(orderRepo.InstanceId);
    }

    #endregion

    #region Scoped Open Generic

    [Test]
    public async Task OpenGeneric_Scoped_SameInstanceWithinScope()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped(typeof(IRepository<>), typeof(Repository<>));
        container.RegisterScoped<IRepository<User>>(static _ => new Repository<User>());
        using var scope = container.CreateScope();

        // Act
        var repo1 = scope.GetService<IRepository<User>>();
        var repo2 = scope.GetService<IRepository<User>>();

        // Assert
        await Assert.That(repo1.InstanceId).IsEqualTo(repo2.InstanceId);
    }

    [Test]
    public async Task OpenGeneric_Scoped_DifferentScopes_DifferentInstances()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<IRepository<User>>(static _ => new Repository<User>());
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        // Act
        var repo1 = scope1.GetService<IRepository<User>>();
        var repo2 = scope2.GetService<IRepository<User>>();

        // Assert
        await Assert.That(repo1.InstanceId).IsNotEqualTo(repo2.InstanceId);
    }

    [Test]
    public async Task OpenGeneric_Scoped_DifferentTypeArgs_IndependentInstances()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<IRepository<User>>(static _ => new Repository<User>());
        container.RegisterScoped<IRepository<Order>>(static _ => new Repository<Order>());
        using var scope = container.CreateScope();

        // Act
        var userRepo1 = scope.GetService<IRepository<User>>();
        var userRepo2 = scope.GetService<IRepository<User>>();
        var orderRepo1 = scope.GetService<IRepository<Order>>();
        var orderRepo2 = scope.GetService<IRepository<Order>>();

        // Assert - Each type arg has its own scoped instance
        await Assert.That(userRepo1.InstanceId).IsEqualTo(userRepo2.InstanceId);
        await Assert.That(orderRepo1.InstanceId).IsEqualTo(orderRepo2.InstanceId);
        await Assert.That(userRepo1.InstanceId).IsNotEqualTo(orderRepo1.InstanceId);
    }

    #endregion

    #region Singleton Open Generic

    [Test]
    public async Task OpenGeneric_Singleton_SameInstanceAlways()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingleton(typeof(IRepository<>), typeof(Repository<>));
        container.RegisterSingleton<IRepository<User>>(static _ => new Repository<User>());
        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        // Act
        var repo1 = scope1.GetService<IRepository<User>>();
        var repo2 = scope2.GetService<IRepository<User>>();

        // Assert
        await Assert.That(repo1.InstanceId).IsEqualTo(repo2.InstanceId);
    }

    [Test]
    public async Task OpenGeneric_Singleton_DifferentTypeArgs_DifferentSingletons()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingleton<IRepository<User>>(static _ => new Repository<User>());
        container.RegisterSingleton<IRepository<Order>>(static _ => new Repository<Order>());
        container.RegisterSingleton<IRepository<Product>>(static _ => new Repository<Product>());
        using var scope = container.CreateScope();

        // Act
        var userRepo = scope.GetService<IRepository<User>>();
        var orderRepo = scope.GetService<IRepository<Order>>();
        var productRepo = scope.GetService<IRepository<Product>>();

        // Assert - Each type argument has its own singleton
        await Assert.That(userRepo.EntityType).IsEqualTo(typeof(User));
        await Assert.That(orderRepo.EntityType).IsEqualTo(typeof(Order));
        await Assert.That(productRepo.EntityType).IsEqualTo(typeof(Product));
        
        var ids = new[] { userRepo.InstanceId, orderRepo.InstanceId, productRepo.InstanceId };
        await Assert.That(ids.Distinct().Count()).IsEqualTo(3);
    }

    #endregion

    #region Open Generic with Dependencies

    [Test]
    public async Task OpenGeneric_WithSingletonDependency_DependencyShared()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingleton<ILogger<User>>(static _ => new Logger<User>());
        container.RegisterTransient<IRepository<User>>(static scope => 
            new RepositoryWithLogger<User>(scope.GetService<ILogger<User>>()));
        using var scope = container.CreateScope();

        // Act
        var repo1 = (RepositoryWithLogger<User>)scope.GetService<IRepository<User>>();
        var repo2 = (RepositoryWithLogger<User>)scope.GetService<IRepository<User>>();

        // Assert - Repos are different (transient), logger is same (singleton)
        await Assert.That(repo1.InstanceId).IsNotEqualTo(repo2.InstanceId);
        await Assert.That(repo1.Logger.InstanceId).IsEqualTo(repo2.Logger.InstanceId);
    }

    #endregion

    #region Multiple Open Generic Registrations

    [Test]
    public async Task MultipleOpenGenerics_GetServices_ReturnsAll()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<IRepository<User>>(static _ => new Repository<User>());
        container.RegisterTransient<IRepository<User>>(static _ => new CachedRepository<User>());
        using var scope = container.CreateScope();

        // Act
        var repos = scope.GetServices<IRepository<User>>().ToList();

        // Assert
        await Assert.That(repos.Count).IsEqualTo(2);
    }

    #endregion
}

#region Additional Generic Test Services

public class RepositoryWithLogger<T>(ILogger<T> logger) : IRepository<T>
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public Type EntityType => typeof(T);
    public ILogger<T> Logger { get; } = logger;
}

public class CachedRepository<T> : IRepository<T>
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public Type EntityType => typeof(T);
}

#endregion
