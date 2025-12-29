namespace Pico.DI.Test;

/// <summary>
/// Tests for type-based Register methods. After enforcing source-generator-only
/// registrations, non-open-generic type-based registration placeholders throw
/// `SourceGeneratorRequiredException` to force generated registrations.
/// </summary>
public class SvcContainerRegisterByTypeTests_New : SvcContainerTestBase
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

    // Additional focused checks
    [Fact]
    public void Register_ServiceAndImplementationType_WithLifetime_Throws()
    {
        var container = new SvcContainer();
        Assert.Throws<Pico.DI.Abs.SourceGeneratorRequiredException>(
            () =>
                container.Register(typeof(IGreeter), typeof(ConsoleGreeter), SvcLifetime.Transient)
        );
    }
}
