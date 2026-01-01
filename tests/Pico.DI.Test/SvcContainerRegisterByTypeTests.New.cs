namespace Pico.DI.Test;

/// <summary>
/// Tests for type-based Register methods.
///
/// DESIGN NOTE: Generic type-based registration placeholders (like RegisterSingleton&lt;T,TImpl&gt;())
/// are now no-op methods that return the container unchanged. They serve as markers for the
/// source generator to scan and generate actual registrations in ConfigureGeneratedServices().
///
/// The non-generic type-based methods that take Type parameters (like Register(typeof(T), ...))
/// still throw SourceGeneratorRequiredException when used with closed generic types, because
/// they are intended for open generic types (like IRepository&lt;&gt;) that can be registered
/// at runtime without factory generation.
/// </summary>
public class SvcContainerRegisterByTypeTests_New : SvcContainerTestBase
{
    [Fact]
    public void Register_ByType_ThrowsWhenNonOpenGeneric()
    {
        // Type-based registration with non-open generics still throws
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () => container.Register(typeof(ConsoleGreeter), SvcLifetime.Singleton)
        );
    }

    [Fact]
    public void RegisterGeneric_ServiceAndImplementation_IsPlaceholder_ReturnsContainer()
    {
        // Generic placeholder method returns container unchanged (no-op)
        var container = new SvcContainer();
        var result = container.Register<IGreeter, ConsoleGreeter>(SvcLifetime.Singleton);
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterTransient_Generic_IsPlaceholder_ReturnsContainer()
    {
        // Generic placeholder method returns container unchanged (no-op)
        var container = new SvcContainer();
        var result = container.RegisterTransient<ConsoleGreeter>();
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterScoped_Generic_IsPlaceholder_ReturnsContainer()
    {
        // Generic placeholder method returns container unchanged (no-op)
        var container = new SvcContainer();
        var result = container.RegisterScoped<ConsoleGreeter>();
        Assert.Same(container, result);
    }

    [Fact]
    public void RegisterSingleton_Generic_IsPlaceholder_ReturnsContainer()
    {
        // Generic placeholder method returns container unchanged (no-op)
        var container = new SvcContainer();
        var result = container.RegisterSingleton<ConsoleGreeter>();
        Assert.Same(container, result);
    }

    // Additional focused checks
    [Fact]
    public void Register_ServiceAndImplementationType_WithLifetime_Throws()
    {
        // Type-based registration with non-open generics still throws
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () =>
                container.Register(typeof(IGreeter), typeof(ConsoleGreeter), SvcLifetime.Transient)
        );
    }
}
