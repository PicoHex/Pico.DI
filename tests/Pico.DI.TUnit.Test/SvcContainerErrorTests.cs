namespace Pico.DI.TUnit.Test;

/// <summary>
/// Tests for error handling and exceptions.
/// </summary>
public class SvcContainerErrorTests : TUnitTestBase
{
    #region Unregistered Service Errors

    [Test]
    public async Task GetService_UnregisteredService_ThrowsPicoDiException()
    {
        // Arrange
        using var container = new SvcContainer();
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IGreeter>());
        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task GetService_UnregisteredService_ErrorMessageContainsTypeName()
    {
        // Arrange
        using var container = new SvcContainer();
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IGreeter>());
        await Assert.That(ex.Message).Contains("IGreeter");
    }

    [Test]
    public async Task GetServices_UnregisteredService_ThrowsPicoDiException()
    {
        // Arrange
        using var container = new SvcContainer();
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetServices<IGreeter>().ToList());
        await Assert.That(ex).IsNotNull();
    }

    #endregion

    #region Open Generic Errors

    [Test]
    public async Task GetService_OpenGenericNotRegistered_ThrowsPicoDiException()
    {
        // Arrange
        using var container = new SvcContainer();
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IRepository<User>>());
        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task GetService_OpenGenericRegistered_ClosedTypeNotDetected_ThrowsWithHint()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient(typeof(IRepository<>), typeof(Repository<>));
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IRepository<User>>());
        await Assert.That(ex.Message).Contains("compile time");
    }

    #endregion

    #region No Factory Errors

    [Test]
    public async Task GetService_NoFactory_ThrowsPicoDiException()
    {
        // Arrange
        using var container = new SvcContainer();
        container.Register(
            new SvcDescriptor(typeof(IGreeter), typeof(ConsoleGreeter), SvcLifetime.Transient)
        );
        using var scope = container.CreateScope();

        // Act & Assert
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IGreeter>());
        await Assert.That(ex.Message).Contains("factory");
    }

    #endregion

    #region SvcDescriptor Validation

    [Test]
    public async Task SvcDescriptor_NullServiceType_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(
            () => new SvcDescriptor(null!, typeof(ConsoleGreeter), SvcLifetime.Transient)
        );
        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task SvcDescriptor_NullInstance_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(
            () => new SvcDescriptor(typeof(IGreeter), (object)null!)
        );
        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task SvcDescriptor_NullFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(
            () =>
                new SvcDescriptor(
                    typeof(IGreeter),
                    (Func<ISvcScope, object>)null!,
                    SvcLifetime.Transient
                )
        );
        await Assert.That(ex).IsNotNull();
    }

    #endregion

    #region SourceGeneratorRequiredException

    [Test]
    public async Task Register_NonOpenGenericByType_ThrowsSourceGeneratorRequiredException()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act & Assert
        var ex = Assert.Throws<SourceGeneratorRequiredException>(
            () =>
                container.Register(typeof(IGreeter), typeof(ConsoleGreeter), SvcLifetime.Transient)
        );
        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task RegisterTransient_NonOpenGenericByType_ThrowsSourceGeneratorRequiredException()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act & Assert
        var ex = Assert.Throws<SourceGeneratorRequiredException>(
            () => container.RegisterTransient(typeof(IGreeter), typeof(ConsoleGreeter))
        );
        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task RegisterScoped_NonOpenGenericByType_ThrowsSourceGeneratorRequiredException()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act & Assert
        var ex = Assert.Throws<SourceGeneratorRequiredException>(
            () => container.RegisterScoped(typeof(IGreeter), typeof(ConsoleGreeter))
        );
        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task RegisterSingleton_NonOpenGenericByType_ThrowsSourceGeneratorRequiredException()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act & Assert
        var ex = Assert.Throws<SourceGeneratorRequiredException>(
            () => container.RegisterSingleton(typeof(IGreeter), typeof(ConsoleGreeter))
        );
        await Assert.That(ex).IsNotNull();
    }

    #endregion

    #region PicoDiException

    [Test]
    public async Task PicoDiException_DefaultConstructor_CreatesInstance()
    {
        // Act
        var exception = new PicoDiException();

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task PicoDiException_WithMessage_SetsMessage()
    {
        // Arrange
        const string message = "Test error message";

        // Act
        var exception = new PicoDiException(message);

        // Assert
        await Assert.That(exception.Message).IsEqualTo(message);
    }

    [Test]
    public async Task PicoDiException_WithInnerException_SetsInnerException()
    {
        // Arrange
        const string message = "Test error message";
        var inner = new InvalidOperationException("Inner exception");

        // Act
        var exception = new PicoDiException(message, inner);

        // Assert
        await Assert.That(exception.Message).IsEqualTo(message);
        await Assert.That(exception.InnerException).IsSameReferenceAs(inner);
    }

    #endregion

    #region SourceGeneratorRequiredException Details

    [Test]
    public async Task SourceGeneratorRequiredException_DefaultConstructor_HasMeaningfulMessage()
    {
        // Act
        var exception = new SourceGeneratorRequiredException();

        // Assert
        await Assert.That(exception.Message).Contains("Compile-time generated registrations");
    }

    [Test]
    public async Task SourceGeneratorRequiredException_CustomMessage_SetsMessage()
    {
        // Arrange
        const string message = "Custom error message";

        // Act
        var exception = new SourceGeneratorRequiredException(message);

        // Assert
        await Assert.That(exception.Message).IsEqualTo(message);
    }

    [Test]
    public async Task SourceGeneratorRequiredException_WithInnerException()
    {
        // Arrange
        const string message = "Custom error message";
        var inner = new InvalidOperationException("Inner");

        // Act
        var exception = new SourceGeneratorRequiredException(message, inner);

        // Assert
        await Assert.That(exception.Message).IsEqualTo(message);
        await Assert.That(exception.InnerException).IsSameReferenceAs(inner);
    }

    #endregion
}
