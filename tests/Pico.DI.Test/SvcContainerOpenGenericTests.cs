namespace Pico.DI.Test;

/// <summary>
/// Tests for open generic type registration.
/// </summary>
public class SvcContainerOpenGenericTests : XUnitTestBase
{
    #region Open Generic Registration Tests

    [Fact]
    public void RegisterTransient_OpenGeneric_RegistersSuccessfully()
    {
        // Arrange
        using var container = CreateContainer();

        // Act - register open generic
        container.RegisterTransient(typeof(IRepository<>), typeof(Repository<>));

        // Assert - container should not throw during registration
        Assert.NotNull(container);
    }

    [Fact]
    public void RegisterScoped_OpenGeneric_RegistersSuccessfully()
    {
        // Arrange
        using var container = CreateContainer();

        // Act - register open generic
        container.RegisterScoped(typeof(IRepository<>), typeof(Repository<>));

        // Assert - container should not throw during registration
        Assert.NotNull(container);
    }

    [Fact]
    public void RegisterSingleton_OpenGeneric_RegistersSuccessfully()
    {
        // Arrange
        using var container = CreateContainer();

        // Act - register open generic
        container.RegisterSingleton(typeof(IRepository<>), typeof(Repository<>));

        // Assert - container should not throw during registration
        Assert.NotNull(container);
    }

    [Fact]
    public void Register_OpenGeneric_WithLifetime_RegistersSuccessfully()
    {
        // Arrange
        using var container = CreateContainer();

        // Act - register open generic with explicit lifetime
        container.Register(typeof(IRepository<>), typeof(Repository<>), SvcLifetime.Transient);

        // Assert - container should not throw during registration
        Assert.NotNull(container);
    }

    [Fact]
    public void RegisterTransient_SingleOpenGeneric_RegistersSuccessfully()
    {
        // Arrange
        using var container = CreateContainer();

        // Act - register open generic with single type parameter
        container.RegisterTransient(typeof(Repository<>));

        // Assert - container should not throw during registration
        Assert.NotNull(container);
    }

    [Fact]
    public void RegisterScoped_SingleOpenGeneric_RegistersSuccessfully()
    {
        // Arrange
        using var container = CreateContainer();

        // Act - register open generic with single type parameter
        container.RegisterScoped(typeof(Repository<>));

        // Assert - container should not throw during registration
        Assert.NotNull(container);
    }

    [Fact]
    public void RegisterSingleton_SingleOpenGeneric_RegistersSuccessfully()
    {
        // Arrange
        using var container = CreateContainer();

        // Act - register open generic with single type parameter
        container.RegisterSingleton(typeof(Repository<>));

        // Assert - container should not throw during registration
        Assert.NotNull(container);
    }

    [Fact]
    public void Register_SingleOpenGeneric_WithLifetime_RegistersSuccessfully()
    {
        // Arrange
        using var container = CreateContainer();

        // Act - register open generic with explicit lifetime
        container.Register(typeof(Repository<>), SvcLifetime.Transient);

        // Assert - container should not throw during registration
        Assert.NotNull(container);
    }

    #endregion

    #region Open Generic Resolution Error Tests

    [Fact]
    public void GetService_OpenGenericWithoutClosedTypeGenerated_ThrowsWithHelpfulMessage()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterTransient(typeof(IRepository<>), typeof(Repository<>));
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IRepository<User>>());

        // Verify message provides helpful guidance
        Assert.Contains("compile time", ex.Message);
        Assert.Contains(nameof(IRepository<User>), ex.Message);
    }

    [Fact]
    public void GetService_OpenGenericNotRegistered_ThrowsWithTypeName()
    {
        // Arrange
        using var container = CreateContainer();
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IRepository<User>>());
        Assert.Contains("IRepository", ex.Message);
    }

    #endregion

    #region SvcDescriptor for Open Generics Tests

    [Fact]
    public void SvcDescriptor_ForOpenGeneric_StoresImplementationType()
    {
        // Arrange & Act
        var descriptor = new SvcDescriptor(
            typeof(IRepository<>),
            typeof(Repository<>),
            SvcLifetime.Transient
        );

        // Assert
        Assert.Equal(typeof(IRepository<>), descriptor.ServiceType);
        Assert.Equal(typeof(Repository<>), descriptor.ImplementationType);
        Assert.Equal(SvcLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void SvcDescriptor_ForOpenGeneric_HasNoFactory()
    {
        // Arrange & Act
        var descriptor = new SvcDescriptor(
            typeof(IRepository<>),
            typeof(Repository<>),
            SvcLifetime.Transient
        );

        // Assert
        Assert.Null(descriptor.Factory);
    }

    #endregion
}
