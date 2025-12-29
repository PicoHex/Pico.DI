namespace Pico.DI.Test;

/// <summary>
/// Tests for type-based Register methods. After enforcing source-generator-only
/// registrations, non-open-generic type-based registration placeholders throw
/// `SourceGeneratorRequiredException` to force generated registrations.
/// </summary>
public class SvcContainerRegisterByTypeTests : SvcContainerTestBase
{
    [Fact]
    public void Register_ByType_ThrowsWhenNonOpenGeneric()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.Register(typeof(ConsoleGreeter), SvcLifetime.Singleton)
        );
    }

    [Fact]
    public void Register_ByType_DoesNotRegister_ButThrows()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.Register(typeof(ConsoleGreeter), SvcLifetime.Singleton)
        );
    }

    [Fact]
    public void RegisterGeneric_ByType_ThrowsWhenNonOpenGeneric()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.Register<ConsoleGreeter>(SvcLifetime.Singleton)
        );
    }

    [Fact]
    public void RegisterGeneric_ServiceAndImplementation_ThrowsWhenNonOpenGeneric()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.Register<IGreeter, ConsoleGreeter>(SvcLifetime.Singleton)
        );
    }

    [Fact]
    public void RegisterTransient_ByType_ThrowsWhenNonOpenGeneric()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterTransient<ConsoleGreeter>()
        );
    }

    [Fact]
    public void RegisterScoped_ByType_ThrowsWhenNonOpenGeneric()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterScoped<ConsoleGreeter>()
        );
    }

    [Fact]
    public void RegisterSingleton_ByType_ThrowsWhenNonOpenGeneric()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterSingleton<ConsoleGreeter>()
        );
    }

    [Fact]
    public void ChainedTypeRegistrations_ThrowsWhenNonOpenGeneric()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () =>
                container
                    .RegisterSingleton<ConsoleGreeter>()
                    .RegisterTransient<AlternativeGreeter>()
                    .RegisterScoped<ConsoleLogger>()
        );
    }

    #region Additional coverage

    [Fact]
    public void Register_ServiceAndImplementationType_WithLifetime_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () =>
                container.Register(typeof(IGreeter), typeof(ConsoleGreeter), SvcLifetime.Transient)
        );
    }

    [Fact]
    public void RegisterGeneric_WithImplementationType_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.Register<IGreeter>(typeof(ConsoleGreeter), SvcLifetime.Singleton)
        );
    }

    [Fact]
    public void RegisterTransient_NonGeneric_ServiceType_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterTransient(typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterTransient_NonGeneric_ServiceAndImplementationType_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterTransient(typeof(IGreeter), typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterTransient_Generic_WithImplementationType_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterTransient<IGreeter>(typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterTransient_Generic_ServiceAndImplementation_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterTransient<IGreeter, ConsoleGreeter>()
        );
    }

    [Fact]
    public void RegisterScoped_NonGeneric_ServiceType_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterScoped(typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterScoped_NonGeneric_ServiceAndImplementationType_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterScoped(typeof(IGreeter), typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterScoped_Generic_WithImplementationType_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterScoped<IGreeter>(typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterScoped_Generic_ServiceAndImplementation_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterScoped<IGreeter, ConsoleGreeter>()
        );
    }

    [Fact]
    public void RegisterSingleton_NonGeneric_ServiceType_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterSingleton(typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterSingleton_NonGeneric_ServiceAndImplementationType_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterSingleton(typeof(IGreeter), typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterSingleton_Generic_WithImplementationType_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterSingleton<IGreeter>(typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterSingleton_Generic_ServiceAndImplementation_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterSingleton<IGreeter, ConsoleGreeter>()
        );
    }

    #endregion
}

namespace Pico.DI.Test;

/// <summary>
/// Tests for type-based Register methods. After enforcing source-generator-only
/// registrations, non-open-generic type-based registration placeholders throw
/// `SourceGeneratorRequiredException` to force generated registrations.
/// </summary>
public class SvcContainerRegisterByTypeTests : SvcContainerTestBase
{
    [Fact]
    public void Register_ByType_ThrowsWhenNonOpenGeneric()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.Register(typeof(ConsoleGreeter), SvcLifetime.Singleton)
        );
    }

    [Fact]
    public void Register_ByType_DoesNotRegister_ButThrows()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.Register(typeof(ConsoleGreeter), SvcLifetime.Singleton)
        );
    }

    [Fact]
    public void RegisterGeneric_ByType_ThrowsWhenNonOpenGeneric()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.Register<ConsoleGreeter>(SvcLifetime.Singleton)
        );
    }

    [Fact]
    public void RegisterGeneric_ServiceAndImplementation_ThrowsWhenNonOpenGeneric()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.Register<IGreeter, ConsoleGreeter>(SvcLifetime.Singleton)
        );
    }

    [Fact]
    public void RegisterTransient_ByType_ThrowsWhenNonOpenGeneric()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterTransient<ConsoleGreeter>()
        );
    }

    [Fact]
    public void RegisterScoped_ByType_ThrowsWhenNonOpenGeneric()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterScoped<ConsoleGreeter>()
        );
    }

    [Fact]
    public void RegisterSingleton_ByType_ThrowsWhenNonOpenGeneric()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterSingleton<ConsoleGreeter>()
        );
    }

    [Fact]
    public void ChainedTypeRegistrations_ThrowsWhenNonOpenGeneric()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () =>
                container
                    .RegisterSingleton<ConsoleGreeter>()
                    .RegisterTransient<AlternativeGreeter>()
                    .RegisterScoped<ConsoleLogger>()
        );
    }

    #region Additional coverage

    [Fact]
    public void Register_ServiceAndImplementationType_WithLifetime_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () =>
                container.Register(typeof(IGreeter), typeof(ConsoleGreeter), SvcLifetime.Transient)
        );
    }

    [Fact]
    public void RegisterGeneric_WithImplementationType_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.Register<IGreeter>(typeof(ConsoleGreeter), SvcLifetime.Singleton)
        );
    }

    [Fact]
    public void RegisterTransient_NonGeneric_ServiceType_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterTransient(typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterTransient_NonGeneric_ServiceAndImplementationType_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterTransient(typeof(IGreeter), typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterTransient_Generic_WithImplementationType_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterTransient<IGreeter>(typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterTransient_Generic_ServiceAndImplementation_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterTransient<IGreeter, ConsoleGreeter>()
        );
    }

    [Fact]
    public void RegisterScoped_NonGeneric_ServiceType_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterScoped(typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterScoped_NonGeneric_ServiceAndImplementationType_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterScoped(typeof(IGreeter), typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterScoped_Generic_WithImplementationType_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterScoped<IGreeter>(typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterScoped_Generic_ServiceAndImplementation_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterScoped<IGreeter, ConsoleGreeter>()
        );
    }

    [Fact]
    public void RegisterSingleton_NonGeneric_ServiceType_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterSingleton(typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterSingleton_NonGeneric_ServiceAndImplementationType_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterSingleton(typeof(IGreeter), typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterSingleton_Generic_WithImplementationType_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterSingleton<IGreeter>(typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterSingleton_Generic_ServiceAndImplementation_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterSingleton<IGreeter, ConsoleGreeter>()
        );
    }

    #endregion
}

namespace Pico.DI.Test;

/// <summary>
/// Tests for Register by Type with Lifetime methods.
/// Note: Type-based registration methods are placeholder methods scanned by Source Generator.
/// These tests verify the placeholder behavior (returning container without registering).
/// </summary>
public class SvcContainerRegisterByTypeTests : SvcContainerTestBase
{
    [Fact]
    public void Register_ByType_ReturnsContainerForChaining()
    {
        // Arrange
        var container = new SvcContainer();

        // Act - Type-based registration returns container for chaining
        var result = container.Register(typeof(ConsoleGreeter), SvcLifetime.Singleton);

        // Assert
        Assert.Same(container, result);
        // Act & Assert - non-open-generic type-based registration now requires source-generated registrations
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.Register(typeof(ConsoleGreeter), SvcLifetime.Singleton)
        );
    }

    [Fact]
    public void Register_ByType_DoesNotActuallyRegister()
    {
        // Arrange
        var container = new SvcContainer();

        // Act - Type-based registration is a placeholder, doesn't register
        container.Register(typeof(ConsoleGreeter), SvcLifetime.Singleton);

        // Assert - Service should not be registered
        using var scope = container.CreateScope();
        Assert.Throws<PicoDiException>(() => scope.GetService(typeof(ConsoleGreeter)));
        // Act & Assert - registration should fail because generator output is required
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.Register(typeof(ConsoleGreeter), SvcLifetime.Singleton)
        );
    }

    [Fact]
    public void RegisterGeneric_ByType_ReturnsContainerForChaining()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.Register<ConsoleGreeter>(SvcLifetime.Singleton);

        // Assert
        Assert.Same(container, result);
        // Act & Assert
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.Register<ConsoleGreeter>(SvcLifetime.Singleton)
        );
    }

    [Fact]
    public void RegisterGeneric_ServiceAndImplementation_ReturnsContainerForChaining()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.Register<IGreeter, ConsoleGreeter>(SvcLifetime.Singleton);

        // Assert
        Assert.Same(container, result);
        // Act & Assert
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.Register<IGreeter, ConsoleGreeter>(SvcLifetime.Singleton)
        );
    }

    [Fact]
    public void RegisterTransient_ByType_ReturnsContainerForChaining()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterTransient<ConsoleGreeter>();

        // Assert
        Assert.Same(container, result);
        // Act & Assert
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterTransient<ConsoleGreeter>()
        );
    }

    [Fact]
    public void RegisterScoped_ByType_ReturnsContainerForChaining()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterScoped<ConsoleGreeter>();

        // Assert
        Assert.Same(container, result);
        // Act & Assert
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterScoped<ConsoleGreeter>()
        );
    }

    [Fact]
    public void RegisterSingleton_ByType_ReturnsContainerForChaining()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterSingleton<ConsoleGreeter>();

        // Assert
        Assert.Same(container, result);
        // Act & Assert
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterSingleton<ConsoleGreeter>()
        );
    }

    [Fact]
    public void ChainedTypeRegistrations_AllReturnContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act - Chain multiple type-based registrations (all placeholders)
        var result = container
            .RegisterSingleton<ConsoleGreeter>()
            .RegisterTransient<AlternativeGreeter>()
            .RegisterScoped<ConsoleLogger>();

        // Assert
        Assert.Same(container, result);
        // Act & Assert - all type-based placeholders now require generator output
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () =>
                container
                    .RegisterSingleton<ConsoleGreeter>()
                    .RegisterTransient<AlternativeGreeter>()
                    .RegisterScoped<ConsoleLogger>()
        );
    }

    #region Additional Type-based Registration Coverage Tests

    [Fact]
    public void Register_ServiceAndImplementationType_WithLifetime_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.Register(
            typeof(IGreeter),
            typeof(ConsoleGreeter),
            SvcLifetime.Transient
        );

        // Assert
        Assert.Same(container, result);
        // Act & Assert
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () =>
                container.Register(typeof(IGreeter), typeof(ConsoleGreeter), SvcLifetime.Transient)
        );
    }

    [Fact]
    public void RegisterGeneric_WithImplementationType_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.Register<IGreeter>(typeof(ConsoleGreeter), SvcLifetime.Singleton);

        // Assert
        Assert.Same(container, result);
        // Act & Assert
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.Register<IGreeter>(typeof(ConsoleGreeter), SvcLifetime.Singleton)
        );
    }

    [Fact]
    public void RegisterTransient_NonGeneric_ServiceType_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterTransient(typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
        // Act & Assert
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterTransient(typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterTransient_NonGeneric_ServiceAndImplementationType_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterTransient(typeof(IGreeter), typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
        // Act & Assert
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterTransient(typeof(IGreeter), typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterTransient_Generic_WithImplementationType_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterTransient<IGreeter>(typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
        // Act & Assert
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterTransient<IGreeter>(typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterTransient_Generic_ServiceAndImplementation_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterTransient<IGreeter, ConsoleGreeter>();

        // Assert
        Assert.Same(container, result);
        // Act & Assert
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterTransient<IGreeter, ConsoleGreeter>()
        );
    }

    [Fact]
    public void RegisterScoped_NonGeneric_ServiceType_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterScoped(typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
        // Act & Assert
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterScoped(typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterScoped_NonGeneric_ServiceAndImplementationType_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterScoped(typeof(IGreeter), typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
        // Act & Assert
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterScoped(typeof(IGreeter), typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterScoped_Generic_WithImplementationType_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterScoped<IGreeter>(typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
        // Act & Assert
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterScoped<IGreeter>(typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterScoped_Generic_ServiceAndImplementation_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterScoped<IGreeter, ConsoleGreeter>();

        // Assert
        Assert.Same(container, result);
        // Act & Assert
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterScoped<IGreeter, ConsoleGreeter>()
        );
    }

    [Fact]
    public void RegisterSingleton_NonGeneric_ServiceType_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterSingleton(typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
        // Act & Assert
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterSingleton(typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterSingleton_NonGeneric_ServiceAndImplementationType_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterSingleton(typeof(IGreeter), typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
        // Act & Assert
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterSingleton(typeof(IGreeter), typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterSingleton_Generic_WithImplementationType_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterSingleton<IGreeter>(typeof(ConsoleGreeter));

        // Assert
        Assert.Same(container, result);
        // Act & Assert
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterSingleton<IGreeter>(typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void RegisterSingleton_Generic_ServiceAndImplementation_ReturnsContainer()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        var result = container.RegisterSingleton<IGreeter, ConsoleGreeter>();

        // Assert
        Assert.Same(container, result);
        // Act & Assert
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.RegisterSingleton<IGreeter, ConsoleGreeter>()
        );
    }

    #endregion
}
