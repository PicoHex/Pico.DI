namespace Pico.DI.Test;

public sealed class SvcLifetimeMatrixTests
{
    public enum RegistrationApi
    {
        // Direct descriptor APIs
        RegisterDescriptor,
        RegisterFactory_NonGeneric,
        RegisterFactory_Generic,
        RegisterFactory_GenericImpl,
        RegisterRange,

        // Lifetime-specific convenience APIs (factory-based)
        RegisterTransient_Factory_NonGeneric,
        RegisterTransient_Factory_Generic,
        RegisterTransient_Factory_GenericImpl,

        RegisterScoped_Factory_NonGeneric,
        RegisterScoped_Factory_Generic,
        RegisterScoped_Factory_GenericImpl,

        RegisterSingleton_Factory_NonGeneric,
        RegisterSingleton_Factory_Generic,
        RegisterSingleton_Factory_GenericImpl,

        // Singleton instance APIs
        RegisterSingle_Instance_NonGeneric,
        RegisterSingle_Instance_Generic,
    }

    public enum ResolutionApi
    {
        GetService_Generic,
        GetService_NonGeneric,
        GetServices_Generic,
        GetServices_NonGeneric,
        CtorInject_Single,
        CtorInject_IEnumerable,
    }

    [Test]
    [MatrixDataSource]
    public async Task Registrations_x_Resolutions_x_Lifetimes_x_IEnumerable(
        [Matrix] RegistrationApi registration,
        [Matrix(SvcLifetime.Transient, SvcLifetime.Scoped, SvcLifetime.Singleton)]
            SvcLifetime requestedLifetime,
        [Matrix] ResolutionApi resolution,
        [Matrix(false, true)] bool multipleRegistrations
    )
    {
        var expectedLifetime = GetEffectiveLifetime(registration, requestedLifetime);

        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        RegisterThing(container, registration, requestedLifetime, multipleRegistrations);

        RegisterConsumersIfNeeded(container, resolution);

        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        var r1a = Resolve(scope1, resolution);
        var r1b = Resolve(scope1, resolution);
        var r2 = Resolve(scope2, resolution);

        AssertNotNull(r1a);
        AssertNotNull(r1b);
        AssertNotNull(r2);

        await AssertLifetimeSemantics(expectedLifetime, r1a, r1b, r2);

        // Additional IEnumerable-specific assertions when the resolution path returns multiple
        if (
            resolution
            is ResolutionApi.GetServices_Generic
                or ResolutionApi.GetServices_NonGeneric
                or ResolutionApi.CtorInject_IEnumerable
        )
        {
            var arr = (object[])r1a;
            await Assert.That(arr.Length).IsEqualTo(multipleRegistrations ? 2 : 1);
        }
    }

    private static void RegisterThing(
        ISvcContainer container,
        RegistrationApi registration,
        SvcLifetime requestedLifetime,
        bool multiple
    )
    {
        static ThingA CreateA() => new();
        static ThingB CreateB() => new();

        void RegisterOnce(Func<IThing> factory, Type implType, object? instance)
        {
            switch (registration)
            {
                case RegistrationApi.RegisterDescriptor:
                    container.Register(
                        new SvcDescriptor(typeof(IThing), _ => factory(), requestedLifetime)
                    );
                    break;

                case RegistrationApi.RegisterFactory_NonGeneric:
                    container.Register(typeof(IThing), _ => factory(), requestedLifetime);
                    break;

                case RegistrationApi.RegisterFactory_Generic:
                    container.Register<IThing>(_ => factory(), requestedLifetime);
                    break;

                case RegistrationApi.RegisterFactory_GenericImpl:
                    container.Register<IThing, IThing>(_ => (IThing)factory(), requestedLifetime);
                    break;

                case RegistrationApi.RegisterRange:
                    container.RegisterRange(
                        [new SvcDescriptor(typeof(IThing), _ => factory(), requestedLifetime)]
                    );
                    break;

                case RegistrationApi.RegisterTransient_Factory_NonGeneric:
                    container.RegisterTransient(typeof(IThing), _ => factory());
                    break;

                case RegistrationApi.RegisterTransient_Factory_Generic:
                    container.RegisterTransient<IThing>(_ => factory());
                    break;

                case RegistrationApi.RegisterTransient_Factory_GenericImpl:
                    container.RegisterTransient<IThing, IThing>(_ => (IThing)factory());
                    break;

                case RegistrationApi.RegisterScoped_Factory_NonGeneric:
                    container.RegisterScoped(typeof(IThing), _ => factory());
                    break;

                case RegistrationApi.RegisterScoped_Factory_Generic:
                    container.RegisterScoped<IThing>(_ => factory());
                    break;

                case RegistrationApi.RegisterScoped_Factory_GenericImpl:
                    container.RegisterScoped<IThing, IThing>(_ => (IThing)factory());
                    break;

                case RegistrationApi.RegisterSingleton_Factory_NonGeneric:
                    container.RegisterSingleton(typeof(IThing), _ => factory());
                    break;

                case RegistrationApi.RegisterSingleton_Factory_Generic:
                    container.RegisterSingleton<IThing>(_ => factory());
                    break;

                case RegistrationApi.RegisterSingleton_Factory_GenericImpl:
                    container.RegisterSingleton<IThing, IThing>(_ => (IThing)factory());
                    break;

                case RegistrationApi.RegisterSingle_Instance_NonGeneric:
                    container.RegisterSingle(typeof(IThing), instance ?? factory());
                    break;

                case RegistrationApi.RegisterSingle_Instance_Generic:
                    container.RegisterSingle<IThing>(instance ?? factory());
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(registration), registration, null);
            }
        }

        // First registration (ThingA)
        RegisterOnce(
            CreateA,
            typeof(ThingA),
            instance: registration
                is RegistrationApi.RegisterSingle_Instance_NonGeneric
                    or RegistrationApi.RegisterSingle_Instance_Generic
                ? new ThingA()
                : null
        );

        // Optional second registration (ThingB)
        if (!multiple)
            return;

        var implType = typeof(ThingB);
        var instanceB = registration
            is RegistrationApi.RegisterSingle_Instance_NonGeneric
                or RegistrationApi.RegisterSingle_Instance_Generic
            ? new ThingB()
            : null;

        RegisterOnce(CreateB, implType, instanceB);
    }

    private static void RegisterConsumersIfNeeded(ISvcContainer container, ResolutionApi resolution)
    {
        switch (resolution)
        {
            case ResolutionApi.CtorInject_Single:
                container.Register(
                    typeof(SingleConsumer),
                    scope => new SingleConsumer(scope.GetService<IThing>()),
                    SvcLifetime.Transient
                );
                break;
            case ResolutionApi.CtorInject_IEnumerable:
                container.Register(
                    typeof(EnumerableConsumer),
                    scope => new EnumerableConsumer(scope.GetServices<IThing>()),
                    SvcLifetime.Transient
                );
                break;
        }
    }

    private static object Resolve(ISvcScope scope, ResolutionApi resolution)
    {
        return resolution switch
        {
            ResolutionApi.GetService_Generic => scope.GetService<IThing>(),
            ResolutionApi.GetService_NonGeneric => (IThing)scope.GetService(typeof(IThing)),
            ResolutionApi.GetServices_Generic
                => scope.GetServices<IThing>().Cast<object>().ToArray(),
            ResolutionApi.GetServices_NonGeneric => scope.GetServices(typeof(IThing)).ToArray(),
            ResolutionApi.CtorInject_Single => scope.GetService<SingleConsumer>().Value,
            ResolutionApi.CtorInject_IEnumerable
                => scope.GetService<EnumerableConsumer>().Values.Cast<object>().ToArray(),
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
        RegistrationApi registration,
        SvcLifetime requested
    )
    {
        return registration switch
        {
            RegistrationApi.RegisterTransient_Factory_NonGeneric
            or RegistrationApi.RegisterTransient_Factory_Generic
            or RegistrationApi.RegisterTransient_Factory_GenericImpl
                => SvcLifetime.Transient,

            RegistrationApi.RegisterScoped_Factory_NonGeneric
            or RegistrationApi.RegisterScoped_Factory_Generic
            or RegistrationApi.RegisterScoped_Factory_GenericImpl
                => SvcLifetime.Scoped,

            RegistrationApi.RegisterSingleton_Factory_NonGeneric
            or RegistrationApi.RegisterSingleton_Factory_Generic
            or RegistrationApi.RegisterSingleton_Factory_GenericImpl
            or RegistrationApi.RegisterSingle_Instance_NonGeneric
            or RegistrationApi.RegisterSingle_Instance_Generic
                => SvcLifetime.Singleton,

            _ => requested,
        };
    }

    public interface IThing
    {
        Guid Id { get; }
    }

    public sealed class ThingA : IThing
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    public sealed class ThingB : IThing
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    public sealed class SingleConsumer
    {
        public SingleConsumer(IThing value) => Value = value;

        public IThing Value { get; }
    }

    public sealed class EnumerableConsumer
    {
        public EnumerableConsumer(IEnumerable<IThing> values) => Values = values;

        public IEnumerable<IThing> Values { get; }
    }
}
