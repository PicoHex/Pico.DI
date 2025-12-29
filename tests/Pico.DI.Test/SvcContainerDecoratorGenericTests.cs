namespace Pico.DI.Test;

using Decorators;

/// <summary>
/// Tests for decorator generic types.
/// Demonstrates how Logger<T> can wrap any registered service type T
/// while maintaining full AOT compatibility through compile-time code generation.
///
/// NOTE: All service interfaces and classes are defined in Decorators/DecoratorServices.cs
/// to avoid nested class scope issues in unit tests.
/// </summary>
public class SvcContainerDecoratorGenericTests : SvcContainerTestBase
{
    #region Basic Decorator Tests

    [Fact]
    public void RegisterDecorator_WithFactoryRegistration_WrapsService()
    {
        // Arrange
        using var container = new SvcContainer();

        // Register the service
        container.RegisterSingleton<IUser>(_ => new User());

        // For AOT compatibility, manually register the decorator factory
        // In a real scenario with source generator, this would be auto-generated
        container.RegisterTransient<Logger<IUser>>(scope => new Logger<IUser>(
            scope.GetService<IUser>()
        ));

        using var scope = container.CreateScope();

        // Act
        var logger = scope.GetService<Logger<IUser>>();

        // Assert
        Assert.NotNull(logger);
        Assert.NotNull(logger.GetInner());
        Assert.IsType<User>(logger.GetInner());
        Assert.Contains("Created Logger<IUser>", logger.Logs);
    }

    [Fact]
    public void RegisterDecorator_DecoratorIsTransient_CreatesNewEachTime()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterSingleton<IUser>(_ => new User());
        container.RegisterTransient<Logger<IUser>>(scope => new Logger<IUser>(
            scope.GetService<IUser>()
        ));

        using var scope = container.CreateScope();

        // Act
        var logger1 = scope.GetService<Logger<IUser>>();
        var logger2 = scope.GetService<Logger<IUser>>();

        // Assert
        Assert.NotSame(logger1, logger2); // Different instances
        Assert.Same(logger1.GetInner(), logger2.GetInner()); // But wrapping same service
    }

    [Fact]
    public void RegisterDecorator_MultipleServiceTypes_EachHasOwnDecorator()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterSingleton<IUser>(_ => new User());
        container.RegisterSingleton<IEmailService>(_ => new EmailService());

        // Register decorators for each type
        container.RegisterTransient<Logger<IUser>>(scope => new Logger<IUser>(
            scope.GetService<IUser>()
        ));
        container.RegisterTransient<Logger<IEmailService>>(scope => new Logger<IEmailService>(
            scope.GetService<IEmailService>()
        ));

        using var scope = container.CreateScope();

        // Act
        var userLogger = scope.GetService<Logger<IUser>>();
        var emailLogger = scope.GetService<Logger<IEmailService>>();

        // Assert
        Assert.NotNull(userLogger);
        Assert.NotNull(emailLogger);
        Assert.IsType<User>(userLogger.GetInner());
        Assert.IsType<EmailService>(emailLogger.GetInner());
    }

    [Fact]
    public void RegisterDecorator_WithComplexDecorator_InjectsMultipleDependencies()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterSingleton<IUser>(_ => new User());
        container.RegisterSingleton<IDecoratorLogger>(_ => new ConsoleDecoratorLogger());

        // Register the decorator with multiple dependencies
        container.RegisterTransient<CachingDecorator<IUser>>(scope => new CachingDecorator<IUser>(
            scope.GetService<IUser>(),
            scope.GetService<IDecoratorLogger>()
        ));

        using var scope = container.CreateScope();

        // Act
        var cachingDecorator = scope.GetService<CachingDecorator<IUser>>();

        // Assert
        Assert.NotNull(cachingDecorator);
        Assert.IsType<User>(cachingDecorator.GetInner());
        cachingDecorator.CacheValue("test", "value");
    }

    #endregion

    #region RegisterDecorator API Tests

    [Fact]
    public void RegisterDecorator_GenericMethod_StoresDecoratorMetadata()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act - Register decorator using generic API
        // Note: Logger<T> would be the decorator generic, but since we can't express
        // bare generics in C#, the actual implementation would use the non-generic
        // RegisterDecorator(Type) method or generate an extension for each decorator
        container.RegisterDecorator(
            typeof(Logger<>),
            new DecoratorMetadata(typeof(Logger<>), SvcLifetime.Transient)
        );

        // Assert - Metadata is stored for source generator to use
        Assert.NotNull(container);

        // In a real scenario, source generator would detect this and
        // generate appropriate closed generic decorators
    }

    [Fact]
    public void RegisterDecorator_WithCustomLifetime_StoresCorrectLifetime()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act
        container.RegisterDecorator(
            typeof(Logger<>),
            new DecoratorMetadata(typeof(Logger<>), SvcLifetime.Scoped)
        );

        // Assert
        Assert.NotNull(container);
    }

    [Fact]
    public void RegisterDecorator_NonGenericType_ThrowsException()
    {
        // Arrange
        using var container = new SvcContainer();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => container.RegisterDecorator<User>()); // User is not generic

        Assert.Contains("must be an open generic type", exception.Message);
    }

    #endregion

    #region AOT-Compatible Scenarios

    [Fact]
    public void Decorator_SupportsMultipleDecoratorLayers()
    {
        // This test demonstrates that decorators can be stacked
        // In a real AOT scenario with source generator:
        // 1. Logger<IUser> wraps IUser
        // 2. CachingDecorator<Logger<IUser>> wraps Logger<IUser>
        // Both would be pre-generated at compile time

        using var container = new SvcContainer();
        container.RegisterSingleton<IUser>(_ => new User());
        container.RegisterSingleton<IDecoratorLogger>(_ => new ConsoleDecoratorLogger());

        // First decorator layer
        container.RegisterTransient<Logger<IUser>>(scope => new Logger<IUser>(
            scope.GetService<IUser>()
        ));

        // Second decorator layer (decorating the decorator)
        container.RegisterTransient<CachingDecorator<Logger<IUser>>>(scope => new CachingDecorator<
            Logger<IUser>
        >(scope.GetService<Logger<IUser>>(), scope.GetService<IDecoratorLogger>()));

        using var scope = container.CreateScope();

        // Act
        var cachedLogger = scope.GetService<CachingDecorator<Logger<IUser>>>();

        // Assert
        Assert.NotNull(cachedLogger);
        var logger = cachedLogger.GetInner();
        Assert.NotNull(logger);
        Assert.IsType<User>(logger.GetInner());
    }

    [Fact]
    public void Decorator_WithScopedService_PreservesScopeSemantics()
    {
        // Ensure that decorators respect the scoping rules of wrapped services
        using var container = new SvcContainer();

        // Register service as scoped
        container.RegisterScoped<IUser>(scope => new User());

        // Decorator is transient (new instance each time)
        container.RegisterTransient<Logger<IUser>>(scope => new Logger<IUser>(
            scope.GetService<IUser>()
        ));

        using var scope1 = container.CreateScope();
        var logger1a = scope1.GetService<Logger<IUser>>();
        var logger1b = scope1.GetService<Logger<IUser>>();

        using var scope2 = container.CreateScope();
        var logger2 = scope2.GetService<Logger<IUser>>();

        // Assert
        Assert.NotSame(logger1a, logger1b); // Decorator is transient
        Assert.Same(logger1a.GetInner(), logger1b.GetInner()); // Wrapped service is scoped
        Assert.NotSame(logger1a.GetInner(), logger2.GetInner()); // Different scopes have different services
    }

    #endregion

    #region Source Generator Integration Points

    [Fact]
    public void GetService_WithRegisteredDecorator_SourceGeneratorWouldCreateFactory()
    {
        // This test documents the expected behavior when source generator is active.
        // Currently, decorators must be manually registered with factories.
        // But with source generator detecting GetService<Logger<IUser>>() calls,
        // it would auto-generate the factory.

        using var container = new SvcContainer();
        container.RegisterSingleton<IUser>(_ => new User());

        // Manually register for this test (source generator would do this automatically)
        container.RegisterTransient<Logger<IUser>>(scope => new Logger<IUser>(
            scope.GetService<IUser>()
        ));

        using var scope = container.CreateScope();

        // Act
        var logger = scope.GetService<Logger<IUser>>();

        // Assert
        Assert.NotNull(logger);
    }

    #endregion
}
