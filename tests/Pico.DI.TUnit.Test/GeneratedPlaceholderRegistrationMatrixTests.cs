using Pico.DI;

namespace Pico.DI.TUnit.Test;

public sealed class GeneratedPlaceholderRegistrationMatrixTests
{
    public enum GeneratedRegistrationApi
    {
        // Lifetime supplied as argument
        Register_Generic,
        Register_Self,
        Register_GenericWithImplementType,

        // Lifetime inferred by method name
        RegisterTransient_Generic,
        RegisterTransient_Self,
        RegisterTransient_GenericWithImplementType,

        RegisterScoped_Generic,
        RegisterScoped_Self,
        RegisterScoped_GenericWithImplementType,

        RegisterSingleton_Generic,
        RegisterSingleton_Self,
        RegisterSingleton_GenericWithImplementType,
    }

    public enum ScopeResolutionApi
    {
        GetService_Generic,
        GetService_NonGeneric,
        GetServices_Generic,
        GetServices_NonGeneric,
    }

    [Test]
    [MatrixDataSource]
    public async Task GeneratedPlaceholderRegistrations_x_ISvcScopeResolutions_x_Lifetimes_x_IEnumerable(
        [Matrix] GeneratedRegistrationApi registration,
        [Matrix(SvcLifetime.Transient, SvcLifetime.Scoped, SvcLifetime.Singleton)]
            SvcLifetime requestedLifetime,
        [Matrix] ScopeResolutionApi resolution,
        [Matrix(false, true)] bool multipleRegistrations
    )
    {
        var effectiveLifetime = GetEffectiveLifetime(registration, requestedLifetime);

        await using var container = new SvcContainer(); // auto-configured via module initializer

        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        var r1a = Resolve(
            scope1,
            registration,
            requestedLifetime,
            resolution,
            multipleRegistrations
        );
        var r1b = Resolve(
            scope1,
            registration,
            requestedLifetime,
            resolution,
            multipleRegistrations
        );
        var r2 = Resolve(
            scope2,
            registration,
            requestedLifetime,
            resolution,
            multipleRegistrations
        );

        AssertNotNull(r1a);
        AssertNotNull(r1b);
        AssertNotNull(r2);

        await AssertLifetimeSemantics(effectiveLifetime, r1a, r1b, r2);

        if (
            resolution
            is ScopeResolutionApi.GetServices_Generic
                or ScopeResolutionApi.GetServices_NonGeneric
        )
        {
            var arr = (object[])r1a;
            var supportsMultiple = SupportsMultiple(registration);
            var expectedCount = multipleRegistrations && supportsMultiple ? 2 : 1;
            await Assert.That(arr.Length).IsEqualTo(expectedCount);
        }
    }

    private static bool SupportsMultiple(GeneratedRegistrationApi api)
    {
        return api switch
        {
            GeneratedRegistrationApi.Register_Generic
            or GeneratedRegistrationApi.Register_GenericWithImplementType
            or GeneratedRegistrationApi.RegisterTransient_Generic
            or GeneratedRegistrationApi.RegisterTransient_GenericWithImplementType
            or GeneratedRegistrationApi.RegisterScoped_Generic
            or GeneratedRegistrationApi.RegisterScoped_GenericWithImplementType
            or GeneratedRegistrationApi.RegisterSingleton_Generic
            or GeneratedRegistrationApi.RegisterSingleton_GenericWithImplementType
                => true,

            _ => false,
        };
    }

    private static object Resolve(
        ISvcScope scope,
        GeneratedRegistrationApi registration,
        SvcLifetime requestedLifetime,
        ScopeResolutionApi resolution,
        bool multiple
    )
    {
        var lifetime = GetEffectiveLifetime(registration, requestedLifetime);

        return (registration, lifetime, multiple) switch
        {
            // Register_Generic (lifetime varies)
            (GeneratedRegistrationApi.Register_Generic, SvcLifetime.Transient, false)
                => ResolveFor<IGenRegisterTransientOne>(scope, resolution),
            (GeneratedRegistrationApi.Register_Generic, SvcLifetime.Transient, true)
                => ResolveFor<IGenRegisterTransientTwo>(scope, resolution),
            (GeneratedRegistrationApi.Register_Generic, SvcLifetime.Scoped, false)
                => ResolveFor<IGenRegisterScopedOne>(scope, resolution),
            (GeneratedRegistrationApi.Register_Generic, SvcLifetime.Scoped, true)
                => ResolveFor<IGenRegisterScopedTwo>(scope, resolution),
            (GeneratedRegistrationApi.Register_Generic, SvcLifetime.Singleton, false)
                => ResolveFor<IGenRegisterSingletonOne>(scope, resolution),
            (GeneratedRegistrationApi.Register_Generic, SvcLifetime.Singleton, true)
                => ResolveFor<IGenRegisterSingletonTwo>(scope, resolution),

            // Register_Self (lifetime varies) — multiple is not representable for self-registrations
            (GeneratedRegistrationApi.Register_Self, SvcLifetime.Transient, _)
                => ResolveFor<GenSelfRegisterTransient>(scope, resolution),
            (GeneratedRegistrationApi.Register_Self, SvcLifetime.Scoped, _)
                => ResolveFor<GenSelfRegisterScoped>(scope, resolution),
            (GeneratedRegistrationApi.Register_Self, SvcLifetime.Singleton, _)
                => ResolveFor<GenSelfRegisterSingleton>(scope, resolution),

            // Register_GenericWithImplementType (lifetime varies)
            (
                GeneratedRegistrationApi.Register_GenericWithImplementType,
                SvcLifetime.Transient,
                false
            )
                => ResolveFor<IGenRegisterTypeTransientOne>(scope, resolution),
            (
                GeneratedRegistrationApi.Register_GenericWithImplementType,
                SvcLifetime.Transient,
                true
            )
                => ResolveFor<IGenRegisterTypeTransientTwo>(scope, resolution),
            (GeneratedRegistrationApi.Register_GenericWithImplementType, SvcLifetime.Scoped, false)
                => ResolveFor<IGenRegisterTypeScopedOne>(scope, resolution),
            (GeneratedRegistrationApi.Register_GenericWithImplementType, SvcLifetime.Scoped, true)
                => ResolveFor<IGenRegisterTypeScopedTwo>(scope, resolution),
            (
                GeneratedRegistrationApi.Register_GenericWithImplementType,
                SvcLifetime.Singleton,
                false
            )
                => ResolveFor<IGenRegisterTypeSingletonOne>(scope, resolution),
            (
                GeneratedRegistrationApi.Register_GenericWithImplementType,
                SvcLifetime.Singleton,
                true
            )
                => ResolveFor<IGenRegisterTypeSingletonTwo>(scope, resolution),

            // Fixed-lifetime variants
            (GeneratedRegistrationApi.RegisterTransient_Generic, _, false)
                => ResolveFor<IGenTransientOne>(scope, resolution),
            (GeneratedRegistrationApi.RegisterTransient_Generic, _, true)
                => ResolveFor<IGenTransientTwo>(scope, resolution),
            (GeneratedRegistrationApi.RegisterTransient_Self, _, _)
                => ResolveFor<GenSelfTransient>(scope, resolution),
            (GeneratedRegistrationApi.RegisterTransient_GenericWithImplementType, _, false)
                => ResolveFor<IGenTransientTypeOne>(scope, resolution),
            (GeneratedRegistrationApi.RegisterTransient_GenericWithImplementType, _, true)
                => ResolveFor<IGenTransientTypeTwo>(scope, resolution),

            (GeneratedRegistrationApi.RegisterScoped_Generic, _, false)
                => ResolveFor<IGenScopedOne>(scope, resolution),
            (GeneratedRegistrationApi.RegisterScoped_Generic, _, true)
                => ResolveFor<IGenScopedTwo>(scope, resolution),
            (GeneratedRegistrationApi.RegisterScoped_Self, _, _)
                => ResolveFor<GenSelfScoped>(scope, resolution),
            (GeneratedRegistrationApi.RegisterScoped_GenericWithImplementType, _, false)
                => ResolveFor<IGenScopedTypeOne>(scope, resolution),
            (GeneratedRegistrationApi.RegisterScoped_GenericWithImplementType, _, true)
                => ResolveFor<IGenScopedTypeTwo>(scope, resolution),

            (GeneratedRegistrationApi.RegisterSingleton_Generic, _, false)
                => ResolveFor<IGenSingletonOne>(scope, resolution),
            (GeneratedRegistrationApi.RegisterSingleton_Generic, _, true)
                => ResolveFor<IGenSingletonTwo>(scope, resolution),
            (GeneratedRegistrationApi.RegisterSingleton_Self, _, _)
                => ResolveFor<GenSelfSingleton>(scope, resolution),
            (GeneratedRegistrationApi.RegisterSingleton_GenericWithImplementType, _, false)
                => ResolveFor<IGenSingletonTypeOne>(scope, resolution),
            (GeneratedRegistrationApi.RegisterSingleton_GenericWithImplementType, _, true)
                => ResolveFor<IGenSingletonTypeTwo>(scope, resolution),

            _ => throw new ArgumentOutOfRangeException(nameof(registration), registration, null)
        };
    }

    private static object ResolveFor<T>(ISvcScope scope, ScopeResolutionApi resolution)
    {
        return resolution switch
        {
            ScopeResolutionApi.GetService_Generic => scope.GetService<T>()!,
            ScopeResolutionApi.GetService_NonGeneric => scope.GetService(typeof(T)),
            ScopeResolutionApi.GetServices_Generic
                => scope.GetServices<T>().Cast<object>().ToArray(),
            ScopeResolutionApi.GetServices_NonGeneric => scope.GetServices(typeof(T)).ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null)
        };
    }

    private static void AssertNotNull(object value)
    {
        if (value is object[] arr)
        {
            if (arr.Length == 0)
                throw new InvalidOperationException("Expected non-empty array");

            foreach (var item in arr)
            {
                if (item is null)
                    throw new InvalidOperationException("Expected non-null item");
            }
            return;
        }

        if (value is null)
            throw new InvalidOperationException("Expected non-null");
    }

    private static async Task AssertLifetimeSemantics(
        SvcLifetime lifetime,
        object first,
        object second,
        object acrossScope
    )
    {
        if (first is object[] a1 && second is object[] a2 && acrossScope is object[] a3)
        {
            await Assert.That(a1.Length).IsEqualTo(a2.Length);
            await Assert.That(a1.Length).IsEqualTo(a3.Length);

            for (var i = 0; i < a1.Length; i++)
            {
                await AssertLifetimeSemantics(lifetime, a1[i], a2[i], a3[i]);
            }

            return;
        }

        var sameWithinScope = ReferenceEquals(first, second);
        var sameAcrossScopes = ReferenceEquals(first, acrossScope);

        switch (lifetime)
        {
            case SvcLifetime.Transient:
                await Assert.That(sameWithinScope).IsFalse();
                await Assert.That(sameAcrossScopes).IsFalse();
                break;

            case SvcLifetime.Scoped:
                await Assert.That(sameWithinScope).IsTrue();
                await Assert.That(sameAcrossScopes).IsFalse();
                break;

            case SvcLifetime.Singleton:
                await Assert.That(sameWithinScope).IsTrue();
                await Assert.That(sameAcrossScopes).IsTrue();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null);
        }
    }

    private static SvcLifetime GetEffectiveLifetime(
        GeneratedRegistrationApi registration,
        SvcLifetime requested
    )
    {
        return registration switch
        {
            GeneratedRegistrationApi.RegisterTransient_Generic
            or GeneratedRegistrationApi.RegisterTransient_Self
            or GeneratedRegistrationApi.RegisterTransient_GenericWithImplementType
                => SvcLifetime.Transient,

            GeneratedRegistrationApi.RegisterScoped_Generic
            or GeneratedRegistrationApi.RegisterScoped_Self
            or GeneratedRegistrationApi.RegisterScoped_GenericWithImplementType
                => SvcLifetime.Scoped,

            GeneratedRegistrationApi.RegisterSingleton_Generic
            or GeneratedRegistrationApi.RegisterSingleton_Self
            or GeneratedRegistrationApi.RegisterSingleton_GenericWithImplementType
                => SvcLifetime.Singleton,

            _ => requested,
        };
    }

    // ----------- Service types (unique per API × lifetime × multiplicity) -----------

    // Register_Generic
    public interface IGenRegisterTransientOne { }

    public interface IGenRegisterTransientTwo { }

    public interface IGenRegisterScopedOne { }

    public interface IGenRegisterScopedTwo { }

    public interface IGenRegisterSingletonOne { }

    public interface IGenRegisterSingletonTwo { }

    public sealed class GenRegisterA
        : IGenRegisterTransientOne,
            IGenRegisterTransientTwo,
            IGenRegisterScopedOne,
            IGenRegisterScopedTwo,
            IGenRegisterSingletonOne,
            IGenRegisterSingletonTwo;

    public sealed class GenRegisterB
        : IGenRegisterTransientTwo,
            IGenRegisterScopedTwo,
            IGenRegisterSingletonTwo;

    // Register_Self
    public sealed class GenSelfRegisterTransient;

    public sealed class GenSelfRegisterScoped;

    public sealed class GenSelfRegisterSingleton;

    // Register_GenericWithImplementType
    public interface IGenRegisterTypeTransientOne { }

    public interface IGenRegisterTypeTransientTwo { }

    public interface IGenRegisterTypeScopedOne { }

    public interface IGenRegisterTypeScopedTwo { }

    public interface IGenRegisterTypeSingletonOne { }

    public interface IGenRegisterTypeSingletonTwo { }

    public sealed class GenRegisterTypeA
        : IGenRegisterTypeTransientOne,
            IGenRegisterTypeTransientTwo,
            IGenRegisterTypeScopedOne,
            IGenRegisterTypeScopedTwo,
            IGenRegisterTypeSingletonOne,
            IGenRegisterTypeSingletonTwo;

    public sealed class GenRegisterTypeB
        : IGenRegisterTypeTransientTwo,
            IGenRegisterTypeScopedTwo,
            IGenRegisterTypeSingletonTwo;

    // Fixed Transient
    public interface IGenTransientOne { }

    public interface IGenTransientTwo { }

    public sealed class GenTransientA : IGenTransientOne, IGenTransientTwo;

    public sealed class GenTransientB : IGenTransientTwo;

    public sealed class GenSelfTransient;

    public interface IGenTransientTypeOne { }

    public interface IGenTransientTypeTwo { }

    public sealed class GenTransientTypeA : IGenTransientTypeOne, IGenTransientTypeTwo;

    public sealed class GenTransientTypeB : IGenTransientTypeTwo;

    // Fixed Scoped
    public interface IGenScopedOne { }

    public interface IGenScopedTwo { }

    public sealed class GenScopedA : IGenScopedOne, IGenScopedTwo;

    public sealed class GenScopedB : IGenScopedTwo;

    public sealed class GenSelfScoped;

    public interface IGenScopedTypeOne { }

    public interface IGenScopedTypeTwo { }

    public sealed class GenScopedTypeA : IGenScopedTypeOne, IGenScopedTypeTwo;

    public sealed class GenScopedTypeB : IGenScopedTypeTwo;

    // Fixed Singleton
    public interface IGenSingletonOne { }

    public interface IGenSingletonTwo { }

    public sealed class GenSingletonA : IGenSingletonOne, IGenSingletonTwo;

    public sealed class GenSingletonB : IGenSingletonTwo;

    public sealed class GenSelfSingleton;

    public interface IGenSingletonTypeOne { }

    public interface IGenSingletonTypeTwo { }

    public sealed class GenSingletonTypeA : IGenSingletonTypeOne, IGenSingletonTypeTwo;

    public sealed class GenSingletonTypeB : IGenSingletonTypeTwo;

    // ----------- Source-scanned registrations (executed by generator, not by tests) -----------

    private static void _PicoDiGen_ScannedRegistrations(ISvcContainer container)
    {
        // Register_Generic (lifetime varies)
        container.Register<IGenRegisterTransientOne, GenRegisterA>(SvcLifetime.Transient);
        container.Register<IGenRegisterTransientTwo, GenRegisterA>(SvcLifetime.Transient);
        container.Register<IGenRegisterTransientTwo, GenRegisterB>(SvcLifetime.Transient);

        container.Register<IGenRegisterScopedOne, GenRegisterA>(SvcLifetime.Scoped);
        container.Register<IGenRegisterScopedTwo, GenRegisterA>(SvcLifetime.Scoped);
        container.Register<IGenRegisterScopedTwo, GenRegisterB>(SvcLifetime.Scoped);

        container.Register<IGenRegisterSingletonOne, GenRegisterA>(SvcLifetime.Singleton);
        container.Register<IGenRegisterSingletonTwo, GenRegisterA>(SvcLifetime.Singleton);
        container.Register<IGenRegisterSingletonTwo, GenRegisterB>(SvcLifetime.Singleton);

        // Register_Self (lifetime varies)
        container.Register<GenSelfRegisterTransient>(SvcLifetime.Transient);
        container.Register<GenSelfRegisterScoped>(SvcLifetime.Scoped);
        container.Register<GenSelfRegisterSingleton>(SvcLifetime.Singleton);

        // Register_GenericWithImplementType (lifetime varies)
        container.Register<IGenRegisterTypeTransientOne>(
            typeof(GenRegisterTypeA),
            SvcLifetime.Transient
        );
        container.Register<IGenRegisterTypeTransientTwo>(
            typeof(GenRegisterTypeA),
            SvcLifetime.Transient
        );
        container.Register<IGenRegisterTypeTransientTwo>(
            typeof(GenRegisterTypeB),
            SvcLifetime.Transient
        );

        container.Register<IGenRegisterTypeScopedOne>(typeof(GenRegisterTypeA), SvcLifetime.Scoped);
        container.Register<IGenRegisterTypeScopedTwo>(typeof(GenRegisterTypeA), SvcLifetime.Scoped);
        container.Register<IGenRegisterTypeScopedTwo>(typeof(GenRegisterTypeB), SvcLifetime.Scoped);

        container.Register<IGenRegisterTypeSingletonOne>(
            typeof(GenRegisterTypeA),
            SvcLifetime.Singleton
        );
        container.Register<IGenRegisterTypeSingletonTwo>(
            typeof(GenRegisterTypeA),
            SvcLifetime.Singleton
        );
        container.Register<IGenRegisterTypeSingletonTwo>(
            typeof(GenRegisterTypeB),
            SvcLifetime.Singleton
        );

        // RegisterTransient_*
        container.RegisterTransient<IGenTransientOne, GenTransientA>();
        container.RegisterTransient<IGenTransientTwo, GenTransientA>();
        container.RegisterTransient<IGenTransientTwo, GenTransientB>();
        container.RegisterTransient<GenSelfTransient>();
        container.RegisterTransient<IGenTransientTypeOne>(typeof(GenTransientTypeA));
        container.RegisterTransient<IGenTransientTypeTwo>(typeof(GenTransientTypeA));
        container.RegisterTransient<IGenTransientTypeTwo>(typeof(GenTransientTypeB));

        // RegisterScoped_*
        container.RegisterScoped<IGenScopedOne, GenScopedA>();
        container.RegisterScoped<IGenScopedTwo, GenScopedA>();
        container.RegisterScoped<IGenScopedTwo, GenScopedB>();
        container.RegisterScoped<GenSelfScoped>();
        container.RegisterScoped<IGenScopedTypeOne>(typeof(GenScopedTypeA));
        container.RegisterScoped<IGenScopedTypeTwo>(typeof(GenScopedTypeA));
        container.RegisterScoped<IGenScopedTypeTwo>(typeof(GenScopedTypeB));

        // RegisterSingleton_*
        container.RegisterSingleton<IGenSingletonOne, GenSingletonA>();
        container.RegisterSingleton<IGenSingletonTwo, GenSingletonA>();
        container.RegisterSingleton<IGenSingletonTwo, GenSingletonB>();
        container.RegisterSingleton<GenSelfSingleton>();
        container.RegisterSingleton<IGenSingletonTypeOne>(typeof(GenSingletonTypeA));
        container.RegisterSingleton<IGenSingletonTypeTwo>(typeof(GenSingletonTypeA));
        container.RegisterSingleton<IGenSingletonTypeTwo>(typeof(GenSingletonTypeB));
    }
}
