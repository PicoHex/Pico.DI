namespace Pico.DI.Benchmarks;

#region Test Services

public interface ILogger
{
    void Log(string message);
}

public class ConsoleLogger : ILogger
{
    public void Log(string message) { }
}

public interface IRepository
{
    void Save();
}

public class Repository : IRepository
{
    public void Save() { }
}

public interface IService
{
    void Execute();
}

public class ServiceA(ILogger logger) : IService
{
    public void Execute() => logger.Log("A");
}

public class ServiceB(IRepository repo) : IService
{
    public void Execute() => repo.Save();
}

public class ServiceC(ILogger logger, IRepository repo) : IService
{
    public void Execute()
    {
        logger.Log("C");
        repo.Save();
    }
}

// Deep dependency chain for stress testing
public interface ILevel1 { }

public interface ILevel2 { }

public interface ILevel3 { }

public interface ILevel4 { }

public interface ILevel5 { }

public class Level1 : ILevel1 { }

public class Level2(ILevel1 l1) : ILevel2
{
    public ILevel1 L1 => l1;
}

public class Level3(ILevel2 l2) : ILevel3
{
    public ILevel2 L2 => l2;
}

public class Level4(ILevel3 l3) : ILevel4
{
    public ILevel3 L3 => l3;
}

public class Level5(ILevel4 l4) : ILevel5
{
    public ILevel4 L4 => l4;
}

#endregion

#region Benchmark Infrastructure

public enum ContainerType
{
    PicoDI,
    MsDI
}

public enum LifetimeType
{
    Transient,
    Scoped,
    Singleton
}

public enum TestScenario
{
    ContainerSetup,
    ScopeCreation,
    SingleResolution,
    MultipleResolutions,
    DeepDependencyChain
}

public record BenchmarkResult(
    ContainerType Container,
    TestScenario Scenario,
    LifetimeType Lifetime,
    int Samples,
    int IterationsPerSample,
    double TotalMs,
    double AvgNs,
    double P50Ns,
    double P90Ns,
    double P95Ns,
    double P99Ns,
    ulong CpuCycles,
    IReadOnlyList<GenCount> GcGenDeltas
);

public static class BenchmarkRunner
{
    private const int DefaultWarmupIterations = 1000;
    private const int DefaultIterations = 10000;
    private const int DefaultSamples = 25;
    private const int DefaultMultipleResolutionsInnerLoop = 100;

    private static int _warmupIterations = DefaultWarmupIterations;
    private static int _samples = DefaultSamples;
    private static int _multipleResolutionsInnerLoop = DefaultMultipleResolutionsInnerLoop;

    public static void Configure(
        int warmupIterations = DefaultWarmupIterations,
        int samples = DefaultSamples,
        int multipleResolutionsInnerLoop = DefaultMultipleResolutionsInnerLoop
    )
    {
        _warmupIterations = warmupIterations;
        _samples = samples;
        _multipleResolutionsInnerLoop = multipleResolutionsInnerLoop;
    }

    // Pre-built containers for resolution tests (avoid container creation overhead)
    private static SvcContainer? _picoContainer;
    private static ServiceProvider? _msProvider;
    private static LifetimeType _currentLifetime;
    private static bool _isDeep;

    public static BenchmarkResult Run(
        ContainerType containerType,
        TestScenario scenario,
        LifetimeType lifetime,
        int iterationsPerSample = DefaultIterations
    )
    {
        var operationsPerOuterIteration =
            scenario == TestScenario.MultipleResolutions ? _multipleResolutionsInnerLoop : 1;

        var operationsPerSample = checked(iterationsPerSample * operationsPerOuterIteration);

        // For resolution tests, pre-build containers
        var needsDeep = scenario == TestScenario.DeepDependencyChain;
        if (
            scenario
            is TestScenario.ScopeCreation
                or TestScenario.SingleResolution
                or TestScenario.MultipleResolutions
                or TestScenario.DeepDependencyChain
        )
        {
            EnsureContainersBuilt(lifetime, needsDeep);
        }

        // Warmup
        if (
            scenario
            is TestScenario.SingleResolution
                or TestScenario.MultipleResolutions
                or TestScenario.DeepDependencyChain
        )
        {
            WarmupResolution(containerType, scenario);
        }
        else
        {
            for (var i = 0; i < _warmupIterations; i++)
                ExecuteScenario(containerType, scenario, lifetime);
        }

        // Sampled timing: run N batches and compute distribution over ns/op.
        // This provides meaningful p50/p90/p95/p99 per test case.
        var nsPerOpSamples = new double[_samples];
        var totalNs = 0d;
        ulong totalCpuCycles = 0;
        IReadOnlyList<GenCount>? lastGcDeltas = null;

        for (var s = 0; s < _samples; s++)
        {
            var summaryName = $"{containerType}/{scenario}/{lifetime}/s{s + 1}";

            var summary = scenario
                is TestScenario.SingleResolution
                    or TestScenario.MultipleResolutions
                    or TestScenario.DeepDependencyChain
                ? TimeResolutionSample(summaryName, iterationsPerSample, containerType, scenario)
                : Runner.Time(
                    summaryName,
                    iterationsPerSample,
                    () => ExecuteScenario(containerType, scenario, lifetime)
                );

            totalNs += summary.ElapsedNanoseconds;
            totalCpuCycles += summary.CpuCycle;
            lastGcDeltas = summary.GenCounts;

            nsPerOpSamples[s] = summary.ElapsedNanoseconds / operationsPerSample;
        }

        Array.Sort(nsPerOpSamples);
        var avgNs = nsPerOpSamples.Average();

        return new BenchmarkResult(
            containerType,
            scenario,
            lifetime,
            _samples,
            operationsPerSample,
            totalNs / 1_000_000d,
            avgNs,
            PercentileSorted(nsPerOpSamples, 0.50),
            PercentileSorted(nsPerOpSamples, 0.90),
            PercentileSorted(nsPerOpSamples, 0.95),
            PercentileSorted(nsPerOpSamples, 0.99),
            totalCpuCycles,
            lastGcDeltas ?? []
        );
    }

    private static void WarmupResolution(ContainerType container, TestScenario scenario)
    {
        if (container == ContainerType.PicoDI)
        {
            using var scope = _picoContainer!.CreateScope();
            for (var i = 0; i < _warmupIterations; i++)
                ExecuteResolutionBody(scope, scenario);
            return;
        }

        using var msScope = _msProvider!.CreateScope();
        for (var i = 0; i < _warmupIterations; i++)
            ExecuteResolutionBody(msScope.ServiceProvider, scenario);
    }

    private static Summary TimeResolutionSample(
        string summaryName,
        int iterationsPerSample,
        ContainerType container,
        TestScenario scenario
    )
    {
        if (container == ContainerType.PicoDI)
        {
            ISvcScope? scope = null;
            return Runner.Time(
                summaryName,
                iterationsPerSample,
                action: () => ExecuteResolutionBody(scope!, scenario),
                setup: () => scope = _picoContainer!.CreateScope(),
                teardown: () =>
                {
                    scope?.Dispose();
                    scope = null;
                }
            );
        }
        else
        {
            IServiceScope? scope = null;
            return Runner.Time(
                summaryName,
                iterationsPerSample,
                action: () => ExecuteResolutionBody(scope!.ServiceProvider, scenario),
                setup: () => scope = _msProvider!.CreateScope(),
                teardown: () =>
                {
                    scope?.Dispose();
                    scope = null;
                }
            );
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExecuteResolutionBody(ISvcScope scope, TestScenario scenario)
    {
        switch (scenario)
        {
            case TestScenario.SingleResolution:
                _ = scope.GetService<IService>();
                return;
            case TestScenario.MultipleResolutions:
                for (var i = 0; i < _multipleResolutionsInnerLoop; i++)
                    _ = scope.GetService<IService>();
                return;
            case TestScenario.DeepDependencyChain:
                _ = scope.GetService<ILevel5>();
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExecuteResolutionBody(
        IServiceProvider serviceProvider,
        TestScenario scenario
    )
    {
        switch (scenario)
        {
            case TestScenario.SingleResolution:
                _ = serviceProvider.GetRequiredService<IService>();
                return;
            case TestScenario.MultipleResolutions:
                for (var i = 0; i < _multipleResolutionsInnerLoop; i++)
                    _ = serviceProvider.GetRequiredService<IService>();
                return;
            case TestScenario.DeepDependencyChain:
                _ = serviceProvider.GetRequiredService<ILevel5>();
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
    }

    // Linear interpolation between closest ranks on the sorted array.
    // p in [0..1].
    private static double PercentileSorted(double[] sorted, double p)
    {
        switch (sorted.Length)
        {
            case 0:
                return double.NaN;
            case 1:
                return sorted[0];
        }

        switch (p)
        {
            case <= 0:
                return sorted[0];
            case >= 1:
                return sorted[^1];
        }

        var pos = (sorted.Length - 1) * p;
        var lo = (int)Math.Floor(pos);
        var hi = (int)Math.Ceiling(pos);
        if (lo == hi)
            return sorted[lo];

        var frac = pos - lo;
        return sorted[lo] + (sorted[hi] - sorted[lo]) * frac;
    }

    private static void EnsureContainersBuilt(LifetimeType lifetime, bool deep)
    {
        if (
            _currentLifetime == lifetime
            && _isDeep == deep
            && _picoContainer != null
            && _msProvider != null
        )
            return;

        // Dispose old containers
        _picoContainer?.Dispose();
        _msProvider?.Dispose();

        // Create new containers
        _picoContainer = new SvcContainer();
        var services = new ServiceCollection();

        if (deep)
        {
            RegisterDeepPicoDI(_picoContainer, lifetime);
            RegisterDeepMsDI(services, lifetime);
        }
        else
        {
            RegisterPicoDI(_picoContainer, lifetime);
            RegisterMsDI(services, lifetime);
        }

        _msProvider = services.BuildServiceProvider();
        _currentLifetime = lifetime;
        _isDeep = deep;
    }

    public static void CleanupContainers()
    {
        _picoContainer?.Dispose();
        _msProvider?.Dispose();
        _picoContainer = null;
        _msProvider = null;
    }

    private static void ExecuteScenario(
        ContainerType container,
        TestScenario scenario,
        LifetimeType lifetime
    )
    {
        switch (scenario)
        {
            case TestScenario.ContainerSetup:
                ExecuteContainerSetup(container, lifetime);
                break;
            case TestScenario.ScopeCreation:
                ExecuteScopeCreation(container, lifetime);
                break;
            case TestScenario.SingleResolution:
                ExecuteSingleResolution(container);
                break;
            case TestScenario.MultipleResolutions:
                ExecuteMultipleResolutions(container);
                break;
            case TestScenario.DeepDependencyChain:
                ExecuteDeepDependencyChain(container);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
    }

    #region Scenario Implementations

    private static void ExecuteContainerSetup(ContainerType container, LifetimeType lifetime)
    {
        if (container == ContainerType.PicoDI)
        {
            using var c = new SvcContainer();
            RegisterPicoDI(c, lifetime);
        }
        else
        {
            var services = new ServiceCollection();
            RegisterMsDI(services, lifetime);
            using var sp = services.BuildServiceProvider();
        }
    }

    private static void ExecuteScopeCreation(ContainerType container, LifetimeType lifetime)
    {
        if (container == ContainerType.PicoDI)
        {
            using var scope = _picoContainer!.CreateScope();
        }
        else
        {
            using var scope = _msProvider!.CreateScope();
        }
    }

    private static void ExecuteSingleResolution(ContainerType container)
    {
        if (container == ContainerType.PicoDI)
        {
            using var scope = _picoContainer!.CreateScope();
            _ = scope.GetService<IService>();
        }
        else
        {
            using var scope = _msProvider!.CreateScope();
            _ = scope.ServiceProvider.GetRequiredService<IService>();
        }
    }

    private static void ExecuteMultipleResolutions(ContainerType container)
    {
        if (container == ContainerType.PicoDI)
        {
            using var scope = _picoContainer!.CreateScope();
            for (var i = 0; i < _multipleResolutionsInnerLoop; i++)
                _ = scope.GetService<IService>();
        }
        else
        {
            using var scope = _msProvider!.CreateScope();
            for (var i = 0; i < _multipleResolutionsInnerLoop; i++)
                _ = scope.ServiceProvider.GetRequiredService<IService>();
        }
    }

    private static void ExecuteDeepDependencyChain(ContainerType container)
    {
        if (container == ContainerType.PicoDI)
        {
            using var scope = _picoContainer!.CreateScope();
            _ = scope.GetService<ILevel5>();
        }
        else
        {
            using var scope = _msProvider!.CreateScope();
            _ = scope.ServiceProvider.GetRequiredService<ILevel5>();
        }
    }

    #endregion

    #region Registration Helpers

    private static void RegisterPicoDI(ISvcContainer c, LifetimeType lifetime)
    {
        switch (lifetime)
        {
            case LifetimeType.Transient:
                c.RegisterTransient<ILogger, ConsoleLogger>();
                c.RegisterTransient<IRepository, Repository>();
                c.RegisterTransient<IService, ServiceC>();
                break;
            case LifetimeType.Scoped:
                c.RegisterScoped<ILogger, ConsoleLogger>();
                c.RegisterScoped<IRepository, Repository>();
                c.RegisterScoped<IService, ServiceC>();
                break;
            case LifetimeType.Singleton:
                c.RegisterSingleton<ILogger, ConsoleLogger>();
                c.RegisterSingleton<IRepository, Repository>();
                c.RegisterSingleton<IService, ServiceC>();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null);
        }
    }

    private static void RegisterMsDI(ServiceCollection services, LifetimeType lifetime)
    {
        switch (lifetime)
        {
            case LifetimeType.Transient:
                services.AddTransient<ILogger, ConsoleLogger>();
                services.AddTransient<IRepository, Repository>();
                services.AddTransient<IService, ServiceC>();
                break;
            case LifetimeType.Scoped:
                services.AddScoped<ILogger, ConsoleLogger>();
                services.AddScoped<IRepository, Repository>();
                services.AddScoped<IService, ServiceC>();
                break;
            case LifetimeType.Singleton:
                services.AddSingleton<ILogger, ConsoleLogger>();
                services.AddSingleton<IRepository, Repository>();
                services.AddSingleton<IService, ServiceC>();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null);
        }
    }

    private static void RegisterDeepPicoDI(ISvcContainer c, LifetimeType lifetime)
    {
        switch (lifetime)
        {
            case LifetimeType.Transient:
                c.RegisterTransient<ILevel1, Level1>();
                c.RegisterTransient<ILevel2, Level2>();
                c.RegisterTransient<ILevel3, Level3>();
                c.RegisterTransient<ILevel4, Level4>();
                c.RegisterTransient<ILevel5, Level5>();
                break;
            case LifetimeType.Scoped:
                c.RegisterScoped<ILevel1, Level1>();
                c.RegisterScoped<ILevel2, Level2>();
                c.RegisterScoped<ILevel3, Level3>();
                c.RegisterScoped<ILevel4, Level4>();
                c.RegisterScoped<ILevel5, Level5>();
                break;
            case LifetimeType.Singleton:
                c.RegisterSingleton<ILevel1, Level1>();
                c.RegisterSingleton<ILevel2, Level2>();
                c.RegisterSingleton<ILevel3, Level3>();
                c.RegisterSingleton<ILevel4, Level4>();
                c.RegisterSingleton<ILevel5, Level5>();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null);
        }
    }

    private static void RegisterDeepMsDI(ServiceCollection services, LifetimeType lifetime)
    {
        switch (lifetime)
        {
            case LifetimeType.Transient:
                services.AddTransient<ILevel1, Level1>();
                services.AddTransient<ILevel2, Level2>();
                services.AddTransient<ILevel3, Level3>();
                services.AddTransient<ILevel4, Level4>();
                services.AddTransient<ILevel5, Level5>();
                break;
            case LifetimeType.Scoped:
                services.AddScoped<ILevel1, Level1>();
                services.AddScoped<ILevel2, Level2>();
                services.AddScoped<ILevel3, Level3>();
                services.AddScoped<ILevel4, Level4>();
                services.AddScoped<ILevel5, Level5>();
                break;
            case LifetimeType.Singleton:
                services.AddSingleton<ILevel1, Level1>();
                services.AddSingleton<ILevel2, Level2>();
                services.AddSingleton<ILevel3, Level3>();
                services.AddSingleton<ILevel4, Level4>();
                services.AddSingleton<ILevel5, Level5>();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null);
        }
    }

    #endregion
}

#endregion

#region Main Program

public static class Program
{
    public static void Main(string[] args)
    {
        var cfg = ParseArgs(args);

        // Create and configure the benchmark session
        var session = new BenchmarkSession(
            title: "Pico.DI vs Microsoft.DI - Code Runner Comparison Benchmark",
            samples: cfg.Samples,
            iterationsPerSample: cfg.IterationsPerSample,
            warmupIterations: cfg.Warmup,
            multipleResolutionsInnerLoop: cfg.MultipleResolutionsInnerLoop
        );

        // Optional: Subscribe to progress events for real-time output
        TestScenario? currentScenario = null;
        session.OnTestCompleted += (_, result, current, total) =>
        {
            if (currentScenario != result.Scenario)
            {
                if (currentScenario != null)
                    Console.WriteLine();
                currentScenario = result.Scenario;
                Console.WriteLine($"▶ Running: {result.Scenario}");
            }
            Console.WriteLine(BenchmarkReportFormatter.FormatProgressLine(result, current, total));
        };

        // Run all benchmarks
        session.Initialize().RunAll();
        Console.WriteLine();

        // Print the complete report with a single call
        session.PrintReport();
    }

    private sealed record BenchmarkConfig(
        int Samples,
        int IterationsPerSample,
        int Warmup,
        int MultipleResolutionsInnerLoop
    );

    private static BenchmarkConfig ParseArgs(string[] args)
    {
        // Keep these defaults in sync with BenchmarkRunner defaults.
        var samples = 25;
        var iterationsPerSample = 10000;
        var warmup = 1000;
        var multi = 100;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "--samples":
                case "-s":
                    samples = NextInt(args, ref i, arg);
                    break;
                case "--iterations":
                case "-i":
                    iterationsPerSample = NextInt(args, ref i, arg);
                    break;
                case "--warmup":
                case "-w":
                    warmup = NextInt(args, ref i, arg);
                    break;
                case "--multi":
                    multi = NextInt(args, ref i, arg);
                    break;
            }

            continue;

            static int NextInt(string[] a, ref int idx, string name)
            {
                if (idx + 1 >= a.Length)
                    throw new ArgumentException($"Missing value for '{name}'.");
                idx++;
                if (!int.TryParse(a[idx], out var v) || v <= 0)
                    throw new ArgumentException($"Invalid value for '{name}': '{a[idx]}'.");
                return v;
            }
        }

        return new BenchmarkConfig(samples, iterationsPerSample, warmup, multi);
    }
}

#endregion
