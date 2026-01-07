namespace Pico.DI.Test;

public sealed class OpenGenericMatrixTests
{
    public enum OpenGenericRegistrationApi
    {
        Register_ByLifetime,
        RegisterTransient,
        RegisterScoped,
        RegisterSingleton,
    }

    public enum ScopeResolutionApi
    {
        GetService_Generic,
        GetService_NonGeneric,
        GetServices_Generic,
        GetServices_NonGeneric,
    }

    // NOTE:
    // Pico.DI.Gen scans *source* Register* calls and generates a module initializer that can auto-configure SvcContainer.
    // To keep matrix cases independent, every (registration API × lifetime × multiplicity) combination uses a unique
    // open-generic service type. That avoids conflicts even though the generator will register everything it finds.

    [Test]
    [MatrixDataSource]
    public async Task OpenGeneric_x_ISvcScopeResolutions_x_Lifetimes_x_IEnumerable(
        [Matrix] OpenGenericRegistrationApi registration,
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
            await Assert.That(arr.Length).IsEqualTo(1);

            if (multipleRegistrations)
            {
                await Assert.That(arr[0] is GenBoxA<int>).IsTrue();
            }
        }
    }

    private static object Resolve(
        ISvcScope scope,
        OpenGenericRegistrationApi registration,
        SvcLifetime requestedLifetime,
        ScopeResolutionApi resolution,
        bool multiple
    )
    {
        var lifetime = GetEffectiveLifetime(registration, requestedLifetime);

        return (registration, lifetime, multiple) switch
        {
            (OpenGenericRegistrationApi.Register_ByLifetime, SvcLifetime.Transient, false)
                => ResolveFor<IGenByLifetimeTransientOne<int>>(scope, resolution),
            (OpenGenericRegistrationApi.Register_ByLifetime, SvcLifetime.Transient, true)
                => ResolveFor<IGenByLifetimeTransientTwo<int>>(scope, resolution),
            (OpenGenericRegistrationApi.Register_ByLifetime, SvcLifetime.Scoped, false)
                => ResolveFor<IGenByLifetimeScopedOne<int>>(scope, resolution),
            (OpenGenericRegistrationApi.Register_ByLifetime, SvcLifetime.Scoped, true)
                => ResolveFor<IGenByLifetimeScopedTwo<int>>(scope, resolution),
            (OpenGenericRegistrationApi.Register_ByLifetime, SvcLifetime.Singleton, false)
                => ResolveFor<IGenByLifetimeSingletonOne<int>>(scope, resolution),
            (OpenGenericRegistrationApi.Register_ByLifetime, SvcLifetime.Singleton, true)
                => ResolveFor<IGenByLifetimeSingletonTwo<int>>(scope, resolution),

            (OpenGenericRegistrationApi.RegisterTransient, _, false)
                => ResolveFor<IGenByTransientOne<int>>(scope, resolution),
            (OpenGenericRegistrationApi.RegisterTransient, _, true)
                => ResolveFor<IGenByTransientTwo<int>>(scope, resolution),

            (OpenGenericRegistrationApi.RegisterScoped, _, false)
                => ResolveFor<IGenByScopedOne<int>>(scope, resolution),
            (OpenGenericRegistrationApi.RegisterScoped, _, true)
                => ResolveFor<IGenByScopedTwo<int>>(scope, resolution),

            (OpenGenericRegistrationApi.RegisterSingleton, _, false)
                => ResolveFor<IGenBySingletonOne<int>>(scope, resolution),
            (OpenGenericRegistrationApi.RegisterSingleton, _, true)
                => ResolveFor<IGenBySingletonTwo<int>>(scope, resolution),

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
        OpenGenericRegistrationApi registration,
        SvcLifetime requested
    )
    {
        return registration switch
        {
            OpenGenericRegistrationApi.RegisterTransient => SvcLifetime.Transient,
            OpenGenericRegistrationApi.RegisterScoped => SvcLifetime.Scoped,
            OpenGenericRegistrationApi.RegisterSingleton => SvcLifetime.Singleton,
            _ => requested,
        };
    }

    // ---------- Types used for open-generic registration ----------

    public interface IGenBox<T>
    {
        Guid Id { get; }
        T Value { get; }
    }

    public sealed class GenBoxA<T>
        : IGenByLifetimeTransientOne<T>,
            IGenByLifetimeTransientTwo<T>,
            IGenByLifetimeScopedOne<T>,
            IGenByLifetimeScopedTwo<T>,
            IGenByLifetimeSingletonOne<T>,
            IGenByLifetimeSingletonTwo<T>,
            IGenByTransientOne<T>,
            IGenByTransientTwo<T>,
            IGenByScopedOne<T>,
            IGenByScopedTwo<T>,
            IGenBySingletonOne<T>,
            IGenBySingletonTwo<T>
    {
        public Guid Id { get; } = Guid.NewGuid();
        public T Value { get; } = default!;
    }

    public sealed class GenBoxB<T>
        : IGenByLifetimeTransientTwo<T>,
            IGenByLifetimeScopedTwo<T>,
            IGenByLifetimeSingletonTwo<T>,
            IGenByTransientTwo<T>,
            IGenByScopedTwo<T>,
            IGenBySingletonTwo<T>
    {
        public Guid Id { get; } = Guid.NewGuid();
        public T Value { get; } = default!;
    }

    // Register_ByLifetime (lifetime varies) — One vs Two registrations
    public interface IGenByLifetimeTransientOne<T> : IGenBox<T>;

    public interface IGenByLifetimeTransientTwo<T> : IGenBox<T>;

    public interface IGenByLifetimeScopedOne<T> : IGenBox<T>;

    public interface IGenByLifetimeScopedTwo<T> : IGenBox<T>;

    public interface IGenByLifetimeSingletonOne<T> : IGenBox<T>;

    public interface IGenByLifetimeSingletonTwo<T> : IGenBox<T>;

    // Fixed-lifetime open-generic registrations
    public interface IGenByTransientOne<T> : IGenBox<T>;

    public interface IGenByTransientTwo<T> : IGenBox<T>;

    public interface IGenByScopedOne<T> : IGenBox<T>;

    public interface IGenByScopedTwo<T> : IGenBox<T>;

    public interface IGenBySingletonOne<T> : IGenBox<T>;

    public interface IGenBySingletonTwo<T> : IGenBox<T>;

    // ---------- Source-scanned registrations (executed by generator, not by tests) ----------

    private static void _PicoDiGen_ScannedRegistrations(ISvcContainer container)
    {
        // RegisterTransient
        container.RegisterTransient(typeof(IGenByTransientOne<>), typeof(GenBoxA<>));
        container.RegisterTransient(typeof(IGenByTransientTwo<>), typeof(GenBoxA<>));
        container.RegisterTransient(typeof(IGenByTransientTwo<>), typeof(GenBoxB<>));

        // RegisterScoped
        container.RegisterScoped(typeof(IGenByScopedOne<>), typeof(GenBoxA<>));
        container.RegisterScoped(typeof(IGenByScopedTwo<>), typeof(GenBoxA<>));
        container.RegisterScoped(typeof(IGenByScopedTwo<>), typeof(GenBoxB<>));

        // RegisterSingleton
        container.RegisterSingleton(typeof(IGenBySingletonOne<>), typeof(GenBoxA<>));
        container.RegisterSingleton(typeof(IGenBySingletonTwo<>), typeof(GenBoxA<>));
        container.RegisterSingleton(typeof(IGenBySingletonTwo<>), typeof(GenBoxB<>));
    }
}
