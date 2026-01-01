namespace Pico.DI.TUnit.Test;

/// <summary>
/// Tests for decorator registration functionality.
/// </summary>
public class SvcContainerDecoratorTests : TUnitTestBase
{
    #region Test Services

    public class LoggingDecorator<T>(T inner, ILogger logger)
        where T : class
    {
        public T Inner { get; } = inner;
        public ILogger Logger { get; } = logger;

        public void LogAndDelegate(string message)
        {
            Logger.Log($"Before: {message}");
            // Delegate to inner
            Logger.Log($"After: {message}");
        }
    }

    public class CachingDecorator<T>(T inner)
        where T : class
    {
        public T Inner { get; } = inner;
        private readonly Dictionary<string, object> _cache = new();

        public object GetOrAdd(string key, Func<object> factory)
        {
            if (_cache.TryGetValue(key, out var value))
                return value;
            value = factory();
            _cache[key] = value;
            return value;
        }
    }

    #endregion

    #region RegisterDecorator Tests

    [Test]
    public async Task RegisterDecorator_OpenGeneric_DoesNotThrow()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - registering open generic decorator should not throw
        // Note: Generic method can't be used with open generic type, use Type overload
        container.RegisterDecorator(typeof(LoggingDecorator<>));

        // Assert
        await Assert.That(container).IsNotNull();
    }

    [Test]
    public async Task RegisterDecorator_NonOpenGeneric_ThrowsArgumentException()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(
            () => container.RegisterDecorator<ConsoleGreeter>()
        );
        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task RegisterDecorator_WithLifetime_DoesNotThrow()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - should not throw
        container.RegisterDecorator(
            typeof(LoggingDecorator<>),
            new DecoratorMetadata(typeof(LoggingDecorator<>), SvcLifetime.Scoped, 0)
        );

        // Assert
        await Assert.That(container).IsNotNull();
    }

    [Test]
    public async Task RegisterDecorator_WithCustomParameterIndex_DoesNotThrow()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act
        container.RegisterDecorator(
            typeof(LoggingDecorator<>),
            new DecoratorMetadata(typeof(LoggingDecorator<>), SvcLifetime.Transient, 0)
        );

        // Assert
        await Assert.That(container).IsNotNull();
    }

    [Test]
    public async Task RegisterDecorator_ReturnsContainerForChaining()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act
        var result = container.RegisterDecorator(typeof(LoggingDecorator<>));

        // Assert
        await Assert.That(result).IsSameReferenceAs(container);
    }

    #endregion

    #region DecoratorMetadata Tests

    [Test]
    public async Task DecoratorMetadata_Constructor_SetsProperties()
    {
        // Act
        var metadata = new DecoratorMetadata(typeof(LoggingDecorator<>), SvcLifetime.Scoped, 1);

        // Assert
        await Assert.That(metadata.DecoratorType).IsEqualTo(typeof(LoggingDecorator<>));
        await Assert.That(metadata.Lifetime).IsEqualTo(SvcLifetime.Scoped);
        await Assert.That(metadata.DecoratedServiceParameterIndex).IsEqualTo(1);
    }

    [Test]
    public async Task DecoratorMetadata_DefaultValues_AreCorrect()
    {
        // Act
        var metadata = new DecoratorMetadata(typeof(LoggingDecorator<>));

        // Assert
        await Assert.That(metadata.Lifetime).IsEqualTo(SvcLifetime.Transient);
        await Assert.That(metadata.DecoratedServiceParameterIndex).IsEqualTo(0);
    }

    [Test]
    public async Task DecoratorMetadata_NullDecoratorType_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new DecoratorMetadata(null!));
        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task DecoratorMetadata_NonOpenGeneric_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(
            () => new DecoratorMetadata(typeof(ConsoleGreeter))
        );
        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task DecoratorMetadata_ClosedGeneric_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(
            () => new DecoratorMetadata(typeof(LoggingDecorator<IGreeter>))
        );
        await Assert.That(ex).IsNotNull();
    }

    #endregion

    #region Container RegisterDecorator with Type Parameter

    [Test]
    public async Task SvcContainer_RegisterDecorator_WithType_DoesNotThrow()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act
        container.RegisterDecorator(typeof(LoggingDecorator<>));

        // Assert
        await Assert.That(container).IsNotNull();
    }

    [Test]
    public async Task SvcContainer_RegisterDecorator_NonOpenGeneric_ThrowsArgumentException()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(
            () => container.RegisterDecorator(typeof(ConsoleGreeter))
        );
        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task SvcContainer_RegisterDecorator_WithMetadata_DoesNotThrow()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act
        container.RegisterDecorator(
            typeof(LoggingDecorator<>),
            new DecoratorMetadata(typeof(LoggingDecorator<>), SvcLifetime.Singleton, 0)
        );

        // Assert
        await Assert.That(container).IsNotNull();
    }

    #endregion
}
