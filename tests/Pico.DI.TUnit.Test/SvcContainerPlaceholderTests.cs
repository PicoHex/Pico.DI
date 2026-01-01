namespace Pico.DI.TUnit.Test;

/// <summary>
/// Tests for placeholder extension methods (scanned by source generator).
/// These methods do nothing at runtime (return container unchanged).
/// </summary>
public class SvcContainerPlaceholderTests : TUnitTestBase
{
    #region Register Placeholder Tests

    [Test]
    public async Task Register_GenericServiceAndImpl_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - placeholder returns container unchanged
        var result = container.Register<IGreeter, ConsoleGreeter>(SvcLifetime.Transient);

        // Assert
        await Assert.That(result).IsSameReferenceAs(container);
    }

    [Test]
    public async Task Register_GenericService_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act
        var result = container.Register<ConsoleGreeter>(SvcLifetime.Transient);

        // Assert
        await Assert.That(result).IsSameReferenceAs(container);
    }

    [Test]
    public async Task Register_GenericServiceWithImplType_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act
        var result = container.Register<IGreeter>(typeof(ConsoleGreeter), SvcLifetime.Transient);

        // Assert
        await Assert.That(result).IsSameReferenceAs(container);
    }

    #endregion

    #region RegisterTransient Placeholder Tests

    [Test]
    public async Task RegisterTransient_GenericServiceAndImpl_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act
        var result = container.RegisterTransient<IGreeter, ConsoleGreeter>();

        // Assert
        await Assert.That(result).IsSameReferenceAs(container);
    }

    [Test]
    public async Task RegisterTransient_GenericService_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act
        var result = container.RegisterTransient<ConsoleGreeter>();

        // Assert
        await Assert.That(result).IsSameReferenceAs(container);
    }

    [Test]
    public async Task RegisterTransient_GenericServiceWithImplType_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act
        var result = container.RegisterTransient<IGreeter>(typeof(ConsoleGreeter));

        // Assert
        await Assert.That(result).IsSameReferenceAs(container);
    }

    #endregion

    #region RegisterScoped Placeholder Tests

    [Test]
    public async Task RegisterScoped_GenericServiceAndImpl_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act
        var result = container.RegisterScoped<IGreeter, ConsoleGreeter>();

        // Assert
        await Assert.That(result).IsSameReferenceAs(container);
    }

    [Test]
    public async Task RegisterScoped_GenericService_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act
        var result = container.RegisterScoped<ConsoleGreeter>();

        // Assert
        await Assert.That(result).IsSameReferenceAs(container);
    }

    [Test]
    public async Task RegisterScoped_GenericServiceWithImplType_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act
        var result = container.RegisterScoped<IGreeter>(typeof(ConsoleGreeter));

        // Assert
        await Assert.That(result).IsSameReferenceAs(container);
    }

    #endregion

    #region RegisterSingleton Placeholder Tests

    [Test]
    public async Task RegisterSingleton_GenericServiceAndImpl_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act
        var result = container.RegisterSingleton<IGreeter, ConsoleGreeter>();

        // Assert
        await Assert.That(result).IsSameReferenceAs(container);
    }

    [Test]
    public async Task RegisterSingleton_GenericService_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act
        var result = container.RegisterSingleton<ConsoleGreeter>();

        // Assert
        await Assert.That(result).IsSameReferenceAs(container);
    }

    [Test]
    public async Task RegisterSingleton_GenericServiceWithImplType_ReturnsContainer()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act
        var result = container.RegisterSingleton<IGreeter>(typeof(ConsoleGreeter));

        // Assert
        await Assert.That(result).IsSameReferenceAs(container);
    }

    #endregion

    #region Chaining Tests

    [Test]
    public async Task Placeholders_CanBeChained()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - all placeholders return container for chaining
        var result = container
            .RegisterTransient<IGreeter, ConsoleGreeter>()
            .RegisterScoped<ILogger, ConsoleLogger>()
            .RegisterSingleton<ConsoleGreeter>();

        // Assert
        await Assert.That(result).IsSameReferenceAs(container);
    }

    [Test]
    public async Task Placeholders_MixedWithFactories_Works()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - mix placeholders with real factory registrations
        container
            .RegisterTransient<IGreeter, ConsoleGreeter>() // placeholder
            .RegisterTransient<ILogger>(_ => new ConsoleLogger()) // real
            .RegisterSingleton<ConsoleGreeter>(); // placeholder

        using var scope = container.CreateScope();

        // Assert - only factory registration works at runtime
        var logger = scope.GetService<ILogger>();
        await Assert.That(logger).IsNotNull();
    }

    #endregion
}
