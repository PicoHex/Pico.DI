namespace Pico.DI.Test;

/// <summary>
/// Tests for error handling and exceptions.
/// </summary>
public class SvcContainerErrorTests : XUnitTestBase
{
    #region Unregistered Service Errors

    [Fact]
    public void GetService_UnregisteredService_ThrowsPicoDiException()
    {
        // Arrange
        using var container = CreateContainer();
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IGreeter>());
        Assert.NotNull(ex);
    }

    [Fact]
    public void GetService_UnregisteredService_ErrorMessageContainsTypeName()
    {
        // Arrange
        using var container = CreateContainer();
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IGreeter>());
        Assert.Contains("IGreeter", ex.Message);
    }

    [Fact]
    public void GetService_ByType_UnregisteredService_ThrowsPicoDiException()
    {
        // Arrange
        using var container = CreateContainer();
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService(typeof(IGreeter)));
        Assert.Contains("IGreeter", ex.Message);
    }

    [Fact]
    public void GetServices_UnregisteredService_ThrowsPicoDiException()
    {
        // Arrange
        using var container = CreateContainer();
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetServices<IGreeter>().ToList());
        Assert.NotNull(ex);
    }

    #endregion

    #region Open Generic Errors

    [Fact]
    public void GetService_OpenGenericNotRegistered_ThrowsPicoDiException()
    {
        // Arrange
        using var container = CreateContainer();
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IRepository<User>>());
        Assert.NotNull(ex);
    }

    [Fact]
    public void GetService_OpenGenericRegistered_ClosedTypeNotDetected_ThrowsWithHint()
    {
        // Arrange
        using var container = CreateContainer();
        container.RegisterTransient(typeof(IRepository<>), typeof(Repository<>));
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IRepository<User>>());
        Assert.Contains("compile time", ex.Message);
    }

    #endregion

    #region No Factory Errors

    [Fact]
    public void GetService_Transient_NoFactory_ThrowsPicoDiException()
    {
        // Arrange
        using var container = CreateContainer();
        container.Register(
            new SvcDescriptor(typeof(IGreeter), typeof(ConsoleGreeter), SvcLifetime.Transient)
        );
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IGreeter>());
        Assert.Contains("factory", ex.Message);
    }

    [Fact]
    public void GetService_Singleton_NoFactory_ThrowsPicoDiException()
    {
        // Arrange
        using var container = CreateContainer();
        container.Register(
            new SvcDescriptor(typeof(IGreeter), typeof(ConsoleGreeter), SvcLifetime.Singleton)
        );
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IGreeter>());
        Assert.Contains("factory", ex.Message);
    }

    [Fact]
    public void GetService_Scoped_NoFactory_ThrowsPicoDiException()
    {
        // Arrange
        using var container = CreateContainer();
        container.Register(
            new SvcDescriptor(typeof(IGreeter), typeof(ConsoleGreeter), SvcLifetime.Scoped)
        );
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IGreeter>());
        Assert.Contains("factory", ex.Message);
    }

    [Fact]
    public void GetServices_Transient_NoFactory_ThrowsPicoDiException()
    {
        // Arrange
        using var container = CreateContainer();
        container.Register(
            new SvcDescriptor(typeof(IGreeter), typeof(ConsoleGreeter), SvcLifetime.Transient)
        );
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetServices<IGreeter>().ToList());
        Assert.Contains("factory", ex.Message);
    }

    #endregion

    #region PicoDiException Tests

    [Fact]
    public void PicoDiException_DefaultConstructor_Works()
    {
        // Act
        var ex = new PicoDiException();

        // Assert
        Assert.NotNull(ex);
    }

    [Fact]
    public void PicoDiException_WithMessage_SetsMessage()
    {
        // Arrange
        var message = "Test error message";

        // Act
        var ex = new PicoDiException(message);

        // Assert
        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public void PicoDiException_WithMessageAndInnerException_SetsProperties()
    {
        // Arrange
        var message = "Test error message";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var ex = new PicoDiException(message, innerException);

        // Assert
        Assert.Equal(message, ex.Message);
        Assert.Same(innerException, ex.InnerException);
    }

    #endregion

    #region SourceGeneratorRequiredException Tests

    [Fact]
    public void SourceGeneratorRequiredException_DefaultConstructor_HasDefaultMessage()
    {
        // Act
        var ex = new SourceGeneratorRequiredException();

        // Assert
        Assert.Contains("Compile-time generated registrations", ex.Message);
    }

    [Fact]
    public void SourceGeneratorRequiredException_WithMessage_SetsMessage()
    {
        // Arrange
        var message = "Custom error message";

        // Act
        var ex = new SourceGeneratorRequiredException(message);

        // Assert
        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public void SourceGeneratorRequiredException_WithMessageAndInner_SetsProperties()
    {
        // Arrange
        var message = "Custom error message";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var ex = new SourceGeneratorRequiredException(message, innerException);

        // Assert
        Assert.Equal(message, ex.Message);
        Assert.Same(innerException, ex.InnerException);
    }

    [Fact]
    public void Register_NonGenericTypeByType_ThrowsSourceGeneratorRequiredException()
    {
        // Arrange
        using var container = CreateContainer();

        // Act & Assert
        Assert.Throws<SourceGeneratorRequiredException>(
            () =>
                container.Register(typeof(IGreeter), typeof(ConsoleGreeter), SvcLifetime.Transient)
        );
    }

    [Fact]
    public void RegisterTransient_NonGenericTypeByType_ThrowsSourceGeneratorRequiredException()
    {
        // Arrange
        using var container = CreateContainer();

        // Act & Assert
        Assert.Throws<SourceGeneratorRequiredException>(
            () => container.RegisterTransient(typeof(IGreeter), typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterScoped_NonGenericTypeByType_ThrowsSourceGeneratorRequiredException()
    {
        // Arrange
        using var container = CreateContainer();

        // Act & Assert
        Assert.Throws<SourceGeneratorRequiredException>(
            () => container.RegisterScoped(typeof(IGreeter), typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterSingleton_NonGenericTypeByType_ThrowsSourceGeneratorRequiredException()
    {
        // Arrange
        using var container = CreateContainer();

        // Act & Assert
        Assert.Throws<SourceGeneratorRequiredException>(
            () => container.RegisterSingleton(typeof(IGreeter), typeof(ConsoleGreeter))
        );
    }

    #endregion
}
