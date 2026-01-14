namespace Pico.DI.Benchmarks;

#region Test Services

// Simple service without dependencies
public interface ISimpleService
{
    void Execute();
}

public class SimpleService : ISimpleService
{
    public void Execute() { }
}

// Service with single dependency
public interface ILogger
{
    void Log(string message);
}

public class ConsoleLogger : ILogger
{
    public void Log(string message) { }
}

public interface IServiceWithDep
{
    void Execute();
}

public class ServiceWithDep(ILogger logger) : IServiceWithDep
{
    public void Execute() => logger.Log("test");
}

// Service with multiple dependencies
public interface IRepository
{
    void Save();
}

public class Repository : IRepository
{
    public void Save() { }
}

public interface IServiceWithMultipleDeps
{
    void Execute();
}

public class ServiceWithMultipleDeps(ILogger logger, IRepository repo) : IServiceWithMultipleDeps
{
    public void Execute()
    {
        logger.Log("C");
        repo.Save();
    }
}

// Deep dependency chain (5 levels)
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

#region Benchmark Categories

/// <summary>
/// Test scenario categories for benchmarking
/// </summary>
public enum TestScenario
{
    /// <summary>Create container + register services + build (measures setup overhead)</summary>
    ContainerSetup,

    /// <summary>CreateScope() only (measures scope creation overhead)</summary>
    ScopeCreation,

    /// <summary>CreateScope() + single GetService call</summary>
    SingleResolution,

    /// <summary>CreateScope() + 100 GetService calls in loop</summary>
    MultipleResolutions
}

/// <summary>
/// Service complexity categories for benchmarking
/// </summary>
public enum ServiceComplexity
{
    /// <summary>Simple service with no dependencies</summary>
    NoDependency,

    /// <summary>Service with 1 dependency</summary>
    SingleDependency,

    /// <summary>Service with 2+ dependencies</summary>
    MultipleDependencies,

    /// <summary>5-level deep dependency chain</summary>
    DeepChain
}

/// <summary>
/// Service lifetime for registration
/// </summary>
public enum Lifetime
{
    Transient,
    Scoped,
    Singleton
}

#endregion

#region Static Factories (Source Generator equivalent)

/// <summary>
/// Pre-compiled static factories - equivalent to what Source Generator produces.
/// These avoid delegate allocation and enable inlining.
/// </summary>
public static class Factories
{
    // No dependency
    public static readonly Func<ISvcScope, ISimpleService> SimpleService =
        static _ => new SimpleService();

    // Single dependency
    public static readonly Func<ISvcScope, ILogger> Logger = static _ => new ConsoleLogger();

    public static readonly Func<ISvcScope, IServiceWithDep> ServiceWithDep =
        static s => new ServiceWithDep(s.GetService<ILogger>());

    // Multiple dependencies
    public static readonly Func<ISvcScope, IRepository> Repository = static _ => new Repository();

    public static readonly Func<ISvcScope, IServiceWithMultipleDeps> ServiceWithMultipleDeps =
        static s => new ServiceWithMultipleDeps(
            s.GetService<ILogger>(),
            s.GetService<IRepository>()
        );

    // Deep chain
    public static readonly Func<ISvcScope, ILevel1> Level1 = static _ => new Level1();
    public static readonly Func<ISvcScope, ILevel2> Level2 = static s => new Level2(
        s.GetService<ILevel1>()
    );
    public static readonly Func<ISvcScope, ILevel3> Level3 = static s => new Level3(
        s.GetService<ILevel2>()
    );
    public static readonly Func<ISvcScope, ILevel4> Level4 = static s => new Level4(
        s.GetService<ILevel3>()
    );
    public static readonly Func<ISvcScope, ILevel5> Level5 = static s => new Level5(
        s.GetService<ILevel4>()
    );
}

#endregion

#region Container Setup

/// <summary>
/// Sets up containers with matching registrations for fair comparison.
/// </summary>
public static class ContainerSetup
{
    public static SvcContainer CreatePicoContainer(ServiceComplexity complexity, Lifetime lifetime)
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        var svcLifetime = ToSvcLifetime(lifetime);

        switch (complexity)
        {
            case ServiceComplexity.NoDependency:
                container.Register(
                    new SvcDescriptor(typeof(ISimpleService), Factories.SimpleService, svcLifetime)
                );
                break;

            case ServiceComplexity.SingleDependency:
                container.Register(
                    new SvcDescriptor(typeof(ILogger), Factories.Logger, svcLifetime)
                );
                container.Register(
                    new SvcDescriptor(
                        typeof(IServiceWithDep),
                        Factories.ServiceWithDep,
                        svcLifetime
                    )
                );
                break;

            case ServiceComplexity.MultipleDependencies:
                container.Register(
                    new SvcDescriptor(typeof(ILogger), Factories.Logger, svcLifetime)
                );
                container.Register(
                    new SvcDescriptor(typeof(IRepository), Factories.Repository, svcLifetime)
                );
                container.Register(
                    new SvcDescriptor(
                        typeof(IServiceWithMultipleDeps),
                        Factories.ServiceWithMultipleDeps,
                        svcLifetime
                    )
                );
                break;

            case ServiceComplexity.DeepChain:
                container.Register(
                    new SvcDescriptor(typeof(ILevel1), Factories.Level1, svcLifetime)
                );
                container.Register(
                    new SvcDescriptor(typeof(ILevel2), Factories.Level2, svcLifetime)
                );
                container.Register(
                    new SvcDescriptor(typeof(ILevel3), Factories.Level3, svcLifetime)
                );
                container.Register(
                    new SvcDescriptor(typeof(ILevel4), Factories.Level4, svcLifetime)
                );
                container.Register(
                    new SvcDescriptor(typeof(ILevel5), Factories.Level5, svcLifetime)
                );
                break;
        }

        container.Build();
        return container;
    }

    public static ServiceProvider CreateMsContainer(ServiceComplexity complexity, Lifetime lifetime)
    {
        var services = new ServiceCollection();

        switch (complexity)
        {
            case ServiceComplexity.NoDependency:
                RegisterMsWithFactory<ISimpleService>(services, lifetime, _ => new SimpleService());
                break;

            case ServiceComplexity.SingleDependency:
                RegisterMsWithFactory<ILogger>(services, lifetime, _ => new ConsoleLogger());
                RegisterMsWithFactory<IServiceWithDep>(
                    services,
                    lifetime,
                    sp => new ServiceWithDep(sp.GetRequiredService<ILogger>())
                );
                break;

            case ServiceComplexity.MultipleDependencies:
                RegisterMsWithFactory<ILogger>(services, lifetime, _ => new ConsoleLogger());
                RegisterMsWithFactory<IRepository>(services, lifetime, _ => new Repository());
                RegisterMsWithFactory<IServiceWithMultipleDeps>(
                    services,
                    lifetime,
                    sp => new ServiceWithMultipleDeps(
                        sp.GetRequiredService<ILogger>(),
                        sp.GetRequiredService<IRepository>()
                    )
                );
                break;

            case ServiceComplexity.DeepChain:
                RegisterMsWithFactory<ILevel1>(services, lifetime, _ => new Level1());
                RegisterMsWithFactory<ILevel2>(
                    services,
                    lifetime,
                    sp => new Level2(sp.GetRequiredService<ILevel1>())
                );
                RegisterMsWithFactory<ILevel3>(
                    services,
                    lifetime,
                    sp => new Level3(sp.GetRequiredService<ILevel2>())
                );
                RegisterMsWithFactory<ILevel4>(
                    services,
                    lifetime,
                    sp => new Level4(sp.GetRequiredService<ILevel3>())
                );
                RegisterMsWithFactory<ILevel5>(
                    services,
                    lifetime,
                    sp => new Level5(sp.GetRequiredService<ILevel4>())
                );
                break;
        }

        return services.BuildServiceProvider();
    }

    private static void RegisterMsWithFactory<TService>(
        ServiceCollection services,
        Lifetime lifetime,
        Func<IServiceProvider, TService> factory
    )
        where TService : class
    {
        switch (lifetime)
        {
            case Lifetime.Transient:
                services.AddTransient(factory);
                break;
            case Lifetime.Scoped:
                services.AddScoped(factory);
                break;
            case Lifetime.Singleton:
                services.AddSingleton(factory);
                break;
        }
    }

    private static SvcLifetime ToSvcLifetime(Lifetime lifetime) =>
        lifetime switch
        {
            Lifetime.Transient => SvcLifetime.Transient,
            Lifetime.Scoped => SvcLifetime.Scoped,
            Lifetime.Singleton => SvcLifetime.Singleton,
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime))
        };
}

#endregion

#region Benchmark Runner

/// <summary>
/// Detailed statistics for a single container's benchmark run.
/// </summary>
public record ContainerStats(
    double Avg,
    double P50,
    double P90,
    double P95,
    double P99,
    double CpuCyclesPerOp,
    int Gen0,
    int Gen1,
    int Gen2
);

public record BenchmarkResult(
    string Name,
    ServiceComplexity Complexity,
    Lifetime Lifetime,
    ContainerStats Pico,
    ContainerStats MsDI,
    double Speedup
);

/// <summary>
/// Result for scenario-based benchmarks (ScopeCreation, ContainerSetup, etc.)
/// </summary>
public record ScenarioBenchmarkResult(
    TestScenario Scenario,
    Lifetime Lifetime,
    ContainerStats Pico,
    ContainerStats MsDI,
    double Speedup
);

public static class Benchmark
{
    private const int WarmupIterations = 1000;
    private const int Samples = 100; // Increased for better percentile accuracy
    private const int IterationsPerSample = 10000;

    public static BenchmarkResult Run(ServiceComplexity complexity, Lifetime lifetime)
    {
        var name = $"{complexity} × {lifetime}";

        // Create containers
        using var picoContainer = ContainerSetup.CreatePicoContainer(complexity, lifetime);
        using var msProvider = ContainerSetup.CreateMsContainer(complexity, lifetime);

        // Get resolve action based on complexity
        var (picoResolve, msResolve) = GetResolveActions(complexity);

        // Warmup
        using (var scope = picoContainer.CreateScope())
        {
            for (int i = 0; i < WarmupIterations; i++)
                picoResolve(scope);
        }
        using (var scope = msProvider.CreateScope())
        {
            for (int i = 0; i < WarmupIterations; i++)
                msResolve(scope.ServiceProvider);
        }

        // Benchmark Pico.DI
        var picoStats = RunContainerBenchmark(
            name,
            "Pico",
            IterationsPerSample,
            () => picoContainer.CreateScope(),
            scope => picoResolve((ISvcScope)scope)
        );

        // Benchmark MS.DI
        var msStats = RunContainerBenchmark(
            name,
            "MsDI",
            IterationsPerSample,
            () => msProvider.CreateScope(),
            scope => msResolve(((IServiceScope)scope).ServiceProvider)
        );

        return new BenchmarkResult(
            name,
            complexity,
            lifetime,
            picoStats,
            msStats,
            msStats.Avg / picoStats.Avg
        );
    }

    private static ContainerStats RunContainerBenchmark(
        string name,
        string containerName,
        int iterationsPerSample,
        Func<IDisposable> createScope,
        Action<IDisposable> resolve
    )
    {
        var results = new double[Samples];
        var cpuCycles = new ulong[Samples];
        int gen0Total = 0,
            gen1Total = 0,
            gen2Total = 0;

        for (int s = 0; s < Samples; s++)
        {
            using var scope = createScope();
            var summary = Runner.Time(
                $"{containerName}/{name}/s{s}",
                iterationsPerSample,
                () => resolve(scope)
            );
            results[s] = summary.ElapsedNanoseconds / iterationsPerSample;
            cpuCycles[s] = summary.CpuCycle;

            foreach (var gc in summary.GenCounts)
            {
                switch (gc.Gen)
                {
                    case 0:
                        gen0Total += gc.Count;
                        break;
                    case 1:
                        gen1Total += gc.Count;
                        break;
                    case 2:
                        gen2Total += gc.Count;
                        break;
                }
            }
        }

        Array.Sort(results);

        var avgCpuCycles = cpuCycles.Average(c => (double)c) / iterationsPerSample;

        return new ContainerStats(
            Avg: results.Average(),
            P50: GetPercentile(results, 50),
            P90: GetPercentile(results, 90),
            P95: GetPercentile(results, 95),
            P99: GetPercentile(results, 99),
            CpuCyclesPerOp: avgCpuCycles,
            Gen0: gen0Total,
            Gen1: gen1Total,
            Gen2: gen2Total
        );
    }

    private static double GetPercentile(double[] sortedData, int percentile)
    {
        var index = (int)Math.Ceiling(percentile / 100.0 * sortedData.Length) - 1;
        return sortedData[Math.Clamp(index, 0, sortedData.Length - 1)];
    }

    private static (Action<ISvcScope> pico, Action<IServiceProvider> ms) GetResolveActions(
        ServiceComplexity complexity
    )
    {
        return complexity switch
        {
            ServiceComplexity.NoDependency
                => (
                    static s => s.GetService<ISimpleService>(),
                    static s => s.GetRequiredService<ISimpleService>()
                ),
            ServiceComplexity.SingleDependency
                => (
                    static s => s.GetService<IServiceWithDep>(),
                    static s => s.GetRequiredService<IServiceWithDep>()
                ),
            ServiceComplexity.MultipleDependencies
                => (
                    static s => s.GetService<IServiceWithMultipleDeps>(),
                    static s => s.GetRequiredService<IServiceWithMultipleDeps>()
                ),
            ServiceComplexity.DeepChain
                => (
                    static s => s.GetService<ILevel5>(),
                    static s => s.GetRequiredService<ILevel5>()
                ),
            _ => throw new ArgumentOutOfRangeException(nameof(complexity))
        };
    }

    #region Scenario-based Benchmarks (ScopeCreation, ContainerSetup, MultipleResolutions)

    private const int MultipleResolutionsInnerLoop = 100;

    /// <summary>
    /// Run scenario-based benchmarks (the old test scenarios)
    /// </summary>
    public static ScenarioBenchmarkResult RunScenario(TestScenario scenario, Lifetime lifetime)
    {
        return scenario switch
        {
            TestScenario.ContainerSetup => RunContainerSetupBenchmark(lifetime),
            TestScenario.ScopeCreation => RunScopeCreationBenchmark(lifetime),
            TestScenario.SingleResolution => RunSingleResolutionBenchmark(lifetime),
            TestScenario.MultipleResolutions => RunMultipleResolutionsBenchmark(lifetime),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
    }

    private static ScenarioBenchmarkResult RunContainerSetupBenchmark(Lifetime lifetime)
    {
        // Warmup
        for (int i = 0; i < WarmupIterations / 10; i++)
        {
            using var c = ContainerSetup.CreatePicoContainer(
                ServiceComplexity.SingleDependency,
                lifetime
            );
            using var p = ContainerSetup.CreateMsContainer(
                ServiceComplexity.SingleDependency,
                lifetime
            );
        }

        // Benchmark Pico.DI container setup
        var picoStats = RunRawBenchmark(
            "ContainerSetup",
            "Pico",
            IterationsPerSample / 10, // Container setup is heavier
            () =>
            {
                using var c = ContainerSetup.CreatePicoContainer(
                    ServiceComplexity.SingleDependency,
                    lifetime
                );
            }
        );

        // Benchmark MS.DI container setup
        var msStats = RunRawBenchmark(
            "ContainerSetup",
            "MsDI",
            IterationsPerSample / 10,
            () =>
            {
                using var p = ContainerSetup.CreateMsContainer(
                    ServiceComplexity.SingleDependency,
                    lifetime
                );
            }
        );

        return new ScenarioBenchmarkResult(
            TestScenario.ContainerSetup,
            lifetime,
            picoStats,
            msStats,
            msStats.Avg / picoStats.Avg
        );
    }

    private static ScenarioBenchmarkResult RunScopeCreationBenchmark(Lifetime lifetime)
    {
        // Pre-build containers
        using var picoContainer = ContainerSetup.CreatePicoContainer(
            ServiceComplexity.SingleDependency,
            lifetime
        );
        using var msProvider = ContainerSetup.CreateMsContainer(
            ServiceComplexity.SingleDependency,
            lifetime
        );

        // Warmup
        for (int i = 0; i < WarmupIterations; i++)
        {
            using var s1 = picoContainer.CreateScope();
            using var s2 = msProvider.CreateScope();
        }

        // Benchmark Pico.DI scope creation (lightweight scope allocation)
        var picoStats = RunRawBenchmark(
            "ScopeCreation",
            "Pico",
            IterationsPerSample,
            () =>
            {
                using var scope = picoContainer.CreateScope();
            }
        );

        // Benchmark MS.DI scope creation
        var msStats = RunRawBenchmark(
            "ScopeCreation",
            "MsDI",
            IterationsPerSample,
            () =>
            {
                using var scope = msProvider.CreateScope();
            }
        );

        return new ScenarioBenchmarkResult(
            TestScenario.ScopeCreation,
            lifetime,
            picoStats,
            msStats,
            msStats.Avg / picoStats.Avg
        );
    }

    private static ScenarioBenchmarkResult RunSingleResolutionBenchmark(Lifetime lifetime)
    {
        // Pre-build containers
        using var picoContainer = ContainerSetup.CreatePicoContainer(
            ServiceComplexity.SingleDependency,
            lifetime
        );
        using var msProvider = ContainerSetup.CreateMsContainer(
            ServiceComplexity.SingleDependency,
            lifetime
        );

        // Warmup (same scope, multiple resolutions - like Part 1)
        using (var scope = picoContainer.CreateScope())
        {
            for (int i = 0; i < WarmupIterations; i++)
                _ = scope.GetService<IServiceWithDep>();
        }
        using (var scope = msProvider.CreateScope())
        {
            for (int i = 0; i < WarmupIterations; i++)
                _ = scope.ServiceProvider.GetRequiredService<IServiceWithDep>();
        }

        // Benchmark Pico.DI: Single scope, multiple resolutions (Part 1 style)
        var picoStats = RunScopedBenchmark(
            "SingleResolution",
            "Pico",
            IterationsPerSample,
            () => picoContainer.CreateScope(),
            scope => ((ISvcScope)scope).GetService<IServiceWithDep>()
        );

        // Benchmark MS.DI: Single scope, multiple resolutions (Part 1 style)
        var msStats = RunScopedBenchmark(
            "SingleResolution",
            "MsDI",
            IterationsPerSample,
            () => msProvider.CreateScope(),
            scope => ((IServiceScope)scope).ServiceProvider.GetRequiredService<IServiceWithDep>()
        );

        return new ScenarioBenchmarkResult(
            TestScenario.SingleResolution,
            lifetime,
            picoStats,
            msStats,
            msStats.Avg / picoStats.Avg
        );
    }

    private static ScenarioBenchmarkResult RunMultipleResolutionsBenchmark(Lifetime lifetime)
    {
        // Pre-build containers
        using var picoContainer = ContainerSetup.CreatePicoContainer(
            ServiceComplexity.SingleDependency,
            lifetime
        );
        using var msProvider = ContainerSetup.CreateMsContainer(
            ServiceComplexity.SingleDependency,
            lifetime
        );

        // Warmup (same scope, multiple resolutions - like Part 1)
        using (var scope = picoContainer.CreateScope())
        {
            for (int i = 0; i < WarmupIterations; i++)
                _ = scope.GetService<IServiceWithDep>();
        }
        using (var scope = msProvider.CreateScope())
        {
            for (int i = 0; i < WarmupIterations; i++)
                _ = scope.ServiceProvider.GetRequiredService<IServiceWithDep>();
        }

        // Benchmark Pico.DI: Single scope, 100 resolutions per iteration (Part 1 style)
        var picoStats = RunScopedBenchmark(
            "MultipleResolutions",
            "Pico",
            IterationsPerSample,
            () => picoContainer.CreateScope(),
            scope =>
            {
                var s = (ISvcScope)scope;
                for (int i = 0; i < MultipleResolutionsInnerLoop; i++)
                    _ = s.GetService<IServiceWithDep>();
            }
        );

        // Benchmark MS.DI: Single scope, 100 resolutions per iteration (Part 1 style)
        var msStats = RunScopedBenchmark(
            "MultipleResolutions",
            "MsDI",
            IterationsPerSample,
            () => msProvider.CreateScope(),
            scope =>
            {
                var sp = ((IServiceScope)scope).ServiceProvider;
                for (int i = 0; i < MultipleResolutionsInnerLoop; i++)
                    _ = sp.GetRequiredService<IServiceWithDep>();
            }
        );

        return new ScenarioBenchmarkResult(
            TestScenario.MultipleResolutions,
            lifetime,
            picoStats,
            msStats,
            msStats.Avg / picoStats.Avg
        );
    }

    /// <summary>
    /// Run benchmark with a single scope per sample (Part 1 style - hot path testing)
    /// </summary>
    private static ContainerStats RunScopedBenchmark(
        string name,
        string containerName,
        int iterationsPerSample,
        Func<IDisposable> createScope,
        Action<IDisposable> resolve
    )
    {
        var results = new double[Samples];
        var cpuCycles = new ulong[Samples];
        int gen0Total = 0,
            gen1Total = 0,
            gen2Total = 0;

        for (int s = 0; s < Samples; s++)
        {
            using var scope = createScope();
            var summary = Runner.Time(
                $"{containerName}/{name}/s{s}",
                iterationsPerSample,
                () => resolve(scope)
            );
            results[s] = summary.ElapsedNanoseconds / iterationsPerSample;
            cpuCycles[s] = summary.CpuCycle;

            foreach (var gc in summary.GenCounts)
            {
                switch (gc.Gen)
                {
                    case 0:
                        gen0Total += gc.Count;
                        break;
                    case 1:
                        gen1Total += gc.Count;
                        break;
                    case 2:
                        gen2Total += gc.Count;
                        break;
                }
            }
        }

        Array.Sort(results);

        var avgCpuCycles = cpuCycles.Average(c => (double)c) / iterationsPerSample;

        return new ContainerStats(
            Avg: results.Average(),
            P50: GetPercentile(results, 50),
            P90: GetPercentile(results, 90),
            P95: GetPercentile(results, 95),
            P99: GetPercentile(results, 99),
            CpuCyclesPerOp: avgCpuCycles,
            Gen0: gen0Total,
            Gen1: gen1Total,
            Gen2: gen2Total
        );
    }

    private static ContainerStats RunRawBenchmark(
        string name,
        string containerName,
        int iterationsPerSample,
        Action action
    )
    {
        var results = new double[Samples];
        var cpuCycles = new ulong[Samples];
        int gen0Total = 0,
            gen1Total = 0,
            gen2Total = 0;

        for (int s = 0; s < Samples; s++)
        {
            var summary = Runner.Time($"{containerName}/{name}/s{s}", iterationsPerSample, action);
            results[s] = summary.ElapsedNanoseconds / iterationsPerSample;
            cpuCycles[s] = summary.CpuCycle;

            foreach (var gc in summary.GenCounts)
            {
                switch (gc.Gen)
                {
                    case 0:
                        gen0Total += gc.Count;
                        break;
                    case 1:
                        gen1Total += gc.Count;
                        break;
                    case 2:
                        gen2Total += gc.Count;
                        break;
                }
            }
        }

        Array.Sort(results);

        var avgCpuCycles = cpuCycles.Average(c => (double)c) / iterationsPerSample;

        return new ContainerStats(
            Avg: results.Average(),
            P50: GetPercentile(results, 50),
            P90: GetPercentile(results, 90),
            P95: GetPercentile(results, 95),
            P99: GetPercentile(results, 99),
            CpuCyclesPerOp: avgCpuCycles,
            Gen0: gen0Total,
            Gen1: gen1Total,
            Gen2: gen2Total
        );
    }

    #endregion
}

#endregion

#region Main Program

public static class Program
{
    public static void Main(string[] args)
    {
        Runner.Initialize();

        Console.WriteLine();
        Console.WriteLine(
            "╔═══════════════════════════════════════════════════════════════════════════════════════════════════════════════╗"
        );
        Console.WriteLine(
            "║                          Pico.DI vs Microsoft.DI - Comprehensive Benchmark                                    ║"
        );
        Console.WriteLine(
            "║                                                                                                               ║"
        );
        Console.WriteLine(
            "║  Part 1: Service Resolution by Complexity (measures GetService<T>() call within a scope)                      ║"
        );
        Console.WriteLine(
            "║  Part 2: Scenario-based Tests (ScopeCreation, ContainerSetup, MultipleResolutions)                            ║"
        );
        Console.WriteLine(
            "║  Environment: .NET 10.0 | AOT | 100 samples × 10,000 iterations                                               ║"
        );
        Console.WriteLine(
            "╚═══════════════════════════════════════════════════════════════════════════════════════════════════════════════╝"
        );
        Console.WriteLine();

        // ==================== Part 1: Service Resolution by Complexity ====================
        Console.WriteLine(
            "═══════════════════════════════════════════════════════════════════════════════════════════════════════════════"
        );
        Console.WriteLine("  PART 1: Service Resolution by Complexity");
        Console.WriteLine(
            "═══════════════════════════════════════════════════════════════════════════════════════════════════════════════"
        );
        Console.WriteLine();

        var complexities = new[]
        {
            ServiceComplexity.NoDependency,
            ServiceComplexity.SingleDependency,
            ServiceComplexity.MultipleDependencies,
            ServiceComplexity.DeepChain
        };

        var lifetimes = new[] { Lifetime.Transient, Lifetime.Scoped, Lifetime.Singleton };

        var results = new List<BenchmarkResult>();
        var totalTests = complexities.Length * lifetimes.Length;
        var currentTest = 0;

        foreach (var complexity in complexities)
        {
            Console.WriteLine($"▶ Testing: {complexity}");
            PrintTableHeader();

            foreach (var lifetime in lifetimes)
            {
                currentTest++;
                Console.Write(
                    $"\r  [{currentTest}/{totalTests}] Running {complexity} × {lifetime}...".PadRight(
                        80
                    )
                );

                var result = Benchmark.Run(complexity, lifetime);
                results.Add(result);

                // Clear progress line and print result
                Console.Write("\r" + new string(' ', 80) + "\r");
                PrintResultRow(result);
            }

            PrintTableFooter();
            Console.WriteLine();
        }

        // ==================== Part 2: Scenario-based Tests ====================
        Console.WriteLine();
        Console.WriteLine(
            "═══════════════════════════════════════════════════════════════════════════════════════════════════════════════"
        );
        Console.WriteLine("  PART 2: Infrastructure Overhead (one-time costs)");
        Console.WriteLine(
            "═══════════════════════════════════════════════════════════════════════════════════════════════════════════════"
        );
        Console.WriteLine();

        // Infrastructure tests - run only once (not per lifetime)
        var infrastructureResults = new List<ScenarioBenchmarkResult>();

        Console.WriteLine("▶ Testing: ContainerSetup (application startup cost)");
        PrintInfrastructureTableHeader();
        Console.Write("\r  Running ContainerSetup...".PadRight(80));
        var containerSetupResult = Benchmark.RunScenario(
            TestScenario.ContainerSetup,
            Lifetime.Scoped
        );
        infrastructureResults.Add(containerSetupResult);
        Console.Write("\r" + new string(' ', 80) + "\r");
        PrintInfrastructureResultRow("ContainerSetup", containerSetupResult);
        PrintTableFooter();
        Console.WriteLine();

        Console.WriteLine("▶ Testing: ScopeCreation (per-request overhead)");
        PrintInfrastructureTableHeader();
        Console.Write("\r  Running ScopeCreation...".PadRight(80));
        var scopeCreationResult = Benchmark.RunScenario(
            TestScenario.ScopeCreation,
            Lifetime.Scoped
        );
        infrastructureResults.Add(scopeCreationResult);
        Console.Write("\r" + new string(' ', 80) + "\r");
        PrintInfrastructureResultRow("ScopeCreation", scopeCreationResult);
        PrintTableFooter();
        Console.WriteLine();

        // ==================== Part 3: Resolution Scenarios ====================
        Console.WriteLine();
        Console.WriteLine(
            "═══════════════════════════════════════════════════════════════════════════════════════════════════════════════"
        );
        Console.WriteLine("  PART 3: Resolution Scenarios (hot path performance)");
        Console.WriteLine(
            "═══════════════════════════════════════════════════════════════════════════════════════════════════════════════"
        );
        Console.WriteLine();

        var resolutionScenarios = new[]
        {
            TestScenario.SingleResolution,
            TestScenario.MultipleResolutions
        };

        var scenarioResults = new List<ScenarioBenchmarkResult>();
        var totalScenarioTests = resolutionScenarios.Length * lifetimes.Length;
        var currentScenarioTest = 0;

        foreach (var scenario in resolutionScenarios)
        {
            Console.WriteLine($"▶ Testing: {scenario}");
            PrintScenarioTableHeader();

            foreach (var lifetime in lifetimes)
            {
                currentScenarioTest++;
                Console.Write(
                    $"\r  [{currentScenarioTest}/{totalScenarioTests}] Running {scenario} × {lifetime}...".PadRight(
                        80
                    )
                );

                var result = Benchmark.RunScenario(scenario, lifetime);
                scenarioResults.Add(result);

                // Clear progress line and print result
                Console.Write("\r" + new string(' ', 80) + "\r");
                PrintScenarioResultRow(result);
            }

            PrintTableFooter();
            Console.WriteLine();
        }

        // Summary
        PrintSummary(results, infrastructureResults, scenarioResults);
    }

    private static void PrintTableHeader()
    {
        Console.WriteLine(
            "┌───────────┬─────────┬──────────┬──────────┬──────────┬──────────┬──────────┬───────────┬─────────────┬──────────┐"
        );
        Console.WriteLine(
            "│ Lifetime  │ Library │ Avg (ns) │ P50 (ns) │ P90 (ns) │ P95 (ns) │ P99 (ns) │ CPU Cycle │ GC (0/1/2)  │ Speedup  │"
        );
        Console.WriteLine(
            "├───────────┼─────────┼──────────┼──────────┼──────────┼──────────┼──────────┼───────────┼─────────────┼──────────┤"
        );
    }

    private static void PrintScenarioTableHeader()
    {
        Console.WriteLine(
            "┌───────────┬─────────┬──────────┬──────────┬──────────┬──────────┬──────────┬───────────┬─────────────┬──────────┐"
        );
        Console.WriteLine(
            "│ Lifetime  │ Library │ Avg (ns) │ P50 (ns) │ P90 (ns) │ P95 (ns) │ P99 (ns) │ CPU Cycle │ GC (0/1/2)  │ Speedup  │"
        );
        Console.WriteLine(
            "├───────────┼─────────┼──────────┼──────────┼──────────┼──────────┼──────────┼───────────┼─────────────┼──────────┤"
        );
    }

    private static void PrintInfrastructureTableHeader()
    {
        Console.WriteLine(
            "┌───────────┬──────────┬──────────┬──────────┬──────────┬──────────┬───────────┬─────────────┬──────────┐"
        );
        Console.WriteLine(
            "│ Library   │ Avg (ns) │ P50 (ns) │ P90 (ns) │ P95 (ns) │ P99 (ns) │ CPU Cycle │ GC (0/1/2)  │ Speedup  │"
        );
        Console.WriteLine(
            "├───────────┼──────────┼──────────┼──────────┼──────────┼──────────┼───────────┼─────────────┼──────────┤"
        );
    }

    private static void PrintResultRow(BenchmarkResult r)
    {
        var p = r.Pico;
        var m = r.MsDI;

        // Pico.DI row
        Console.WriteLine(
            $"│ {r.Lifetime, -9} │ {"Pico", -7} │ {p.Avg, 8:F1} │ {p.P50, 8:F1} │ {p.P90, 8:F1} │ {p.P95, 8:F1} │ {p.P99, 8:F1} │ {p.CpuCyclesPerOp, 9:F0} │ {p.Gen0, 3}/{p.Gen1, 2}/{p.Gen2, 2}     │ {r.Speedup, 7:F2}x │"
        );
        // MS.DI row
        Console.WriteLine(
            $"│           │ {"Ms.DI", -7} │ {m.Avg, 8:F1} │ {m.P50, 8:F1} │ {m.P90, 8:F1} │ {m.P95, 8:F1} │ {m.P99, 8:F1} │ {m.CpuCyclesPerOp, 9:F0} │ {m.Gen0, 3}/{m.Gen1, 2}/{m.Gen2, 2}     │          │"
        );
        Console.WriteLine(
            "├───────────┼─────────┼──────────┼──────────┼──────────┼──────────┼──────────┼───────────┼─────────────┼──────────┤"
        );
    }

    private static void PrintScenarioResultRow(ScenarioBenchmarkResult r)
    {
        var p = r.Pico;
        var m = r.MsDI;

        // Highlight the speedup if it's very high
        var speedupStr = r.Speedup >= 10 ? $"★{r.Speedup, 6:F1}x" : $" {r.Speedup, 6:F2}x";

        // Pico.DI row
        Console.WriteLine(
            $"│ {r.Lifetime, -9} │ {"Pico", -7} │ {p.Avg, 8:F1} │ {p.P50, 8:F1} │ {p.P90, 8:F1} │ {p.P95, 8:F1} │ {p.P99, 8:F1} │ {p.CpuCyclesPerOp, 9:F0} │ {p.Gen0, 3}/{p.Gen1, 2}/{p.Gen2, 2}     │{speedupStr} │"
        );
        // MS.DI row
        Console.WriteLine(
            $"│           │ {"Ms.DI", -7} │ {m.Avg, 8:F1} │ {m.P50, 8:F1} │ {m.P90, 8:F1} │ {m.P95, 8:F1} │ {m.P99, 8:F1} │ {m.CpuCyclesPerOp, 9:F0} │ {m.Gen0, 3}/{m.Gen1, 2}/{m.Gen2, 2}     │          │"
        );
        Console.WriteLine(
            "├───────────┼─────────┼──────────┼──────────┼──────────┼──────────┼──────────┼───────────┼─────────────┼──────────┤"
        );
    }

    private static void PrintInfrastructureResultRow(string name, ScenarioBenchmarkResult r)
    {
        var p = r.Pico;
        var m = r.MsDI;

        var speedupStr = r.Speedup >= 10 ? $"★{r.Speedup, 6:F1}x" : $" {r.Speedup, 6:F2}x";

        // Pico.DI row
        Console.WriteLine(
            $"│ {"Pico.DI", -9} │ {p.Avg, 8:F1} │ {p.P50, 8:F1} │ {p.P90, 8:F1} │ {p.P95, 8:F1} │ {p.P99, 8:F1} │ {p.CpuCyclesPerOp, 9:F0} │ {p.Gen0, 3}/{p.Gen1, 2}/{p.Gen2, 2}     │{speedupStr} │"
        );
        // MS.DI row
        Console.WriteLine(
            $"│ {"MS.DI", -9} │ {m.Avg, 8:F1} │ {m.P50, 8:F1} │ {m.P90, 8:F1} │ {m.P95, 8:F1} │ {m.P99, 8:F1} │ {m.CpuCyclesPerOp, 9:F0} │ {m.Gen0, 3}/{m.Gen1, 2}/{m.Gen2, 2}     │          │"
        );
        Console.WriteLine(
            "├───────────┼──────────┼──────────┼──────────┼──────────┼──────────┼───────────┼─────────────┼──────────┤"
        );
    }

    private static void PrintTableFooter()
    {
        // Remove last separator and replace with footer
        Console.SetCursorPosition(0, Console.CursorTop - 1);
        Console.WriteLine(
            "└───────────┴─────────┴──────────┴──────────┴──────────┴──────────┴──────────┴───────────┴─────────────┴──────────┘"
        );
    }

    private static void PrintSummary(
        List<BenchmarkResult> results,
        List<ScenarioBenchmarkResult> infrastructureResults,
        List<ScenarioBenchmarkResult> scenarioResults
    )
    {
        Console.WriteLine(
            "╔═══════════════════════════════════════════════════════════════════════════════════════════════════════════════╗"
        );
        Console.WriteLine(
            "║                                              SUMMARY                                                          ║"
        );
        Console.WriteLine(
            "╚═══════════════════════════════════════════════════════════════════════════════════════════════════════════════╝"
        );
        Console.WriteLine();

        // Part 1 Summary
        Console.WriteLine("▶ Part 1: Service Resolution - Average Speedup by Lifetime:");
        foreach (var lifetime in Enum.GetValues<Lifetime>())
        {
            var avg = results.Where(r => r.Lifetime == lifetime).Average(r => r.Speedup);
            Console.WriteLine($"   {lifetime, -12}: {avg:F2}x faster");
        }
        Console.WriteLine();

        Console.WriteLine("▶ Part 1: Service Resolution - Average Speedup by Complexity:");
        foreach (var complexity in Enum.GetValues<ServiceComplexity>())
        {
            var avg = results.Where(r => r.Complexity == complexity).Average(r => r.Speedup);
            Console.WriteLine($"   {complexity, -22}: {avg:F2}x faster");
        }
        Console.WriteLine();

        // Part 2 Summary (Infrastructure)
        Console.WriteLine("▶ Part 2: Infrastructure Overhead:");
        foreach (var r in infrastructureResults)
        {
            Console.WriteLine($"   {r.Scenario, -22}: {r.Speedup:F2}x faster");
        }
        Console.WriteLine();

        // Part 3 Summary (Resolution Scenarios)
        Console.WriteLine("▶ Part 3: Resolution Scenarios - Average Speedup:");
        foreach (
            var scenario in new[]
            {
                TestScenario.SingleResolution,
                TestScenario.MultipleResolutions
            }
        )
        {
            var scenarioData = scenarioResults.Where(r => r.Scenario == scenario);
            if (scenarioData.Any())
            {
                var avg = scenarioData.Average(r => r.Speedup);
                Console.WriteLine($"   {scenario, -22}: {avg:F2}x faster");
            }
        }
        Console.WriteLine();

        // Overall (only Part 1 and Part 3 for "hot path" performance)
        var hotPathSpeedups = results
            .Select(r => r.Speedup)
            .Concat(scenarioResults.Select(r => r.Speedup))
            .ToList();
        var overallAvg = hotPathSpeedups.Average();
        var picoWins = hotPathSpeedups.Count(s => s > 1);
        var totalComparisons = hotPathSpeedups.Count;
        var maxSpeedup = hotPathSpeedups.Max();

        Console.WriteLine(
            "╔═══════════════════════════════════════════════════════════════════════════════════════════════════════════════╗"
        );
        Console.WriteLine(
            $"║  Hot Path Performance: Pico.DI is {overallAvg:F2}x faster on average (max: {maxSpeedup:F1}x)                                ║"
        );
        Console.WriteLine(
            $"║  Pico.DI wins: {picoWins} / {totalComparisons} scenarios                                                                           ║"
        );
        Console.WriteLine(
            "╚═══════════════════════════════════════════════════════════════════════════════════════════════════════════════╝"
        );

        // Detailed tables
        Console.WriteLine();
        Console.WriteLine("▶ Part 1 Results - Service Resolution (Avg ns):");
        Console.WriteLine(
            "┌─────────────────────────────────────┬──────────┬──────────┬──────────┐"
        );
        Console.WriteLine(
            "│ Test Case                           │ Pico(ns) │ MsDI(ns) │ Speedup  │"
        );
        Console.WriteLine(
            "├─────────────────────────────────────┼──────────┼──────────┼──────────┤"
        );

        foreach (var r in results)
        {
            var testName = $"{r.Complexity} × {r.Lifetime}";
            Console.WriteLine(
                $"│ {testName, -35} │{r.Pico.Avg, 9:F1} │{r.MsDI.Avg, 9:F1} │ {r.Speedup, 7:F2}x │"
            );
        }

        Console.WriteLine(
            "└─────────────────────────────────────┴──────────┴──────────┴──────────┘"
        );

        Console.WriteLine();
        Console.WriteLine("▶ Part 2 Results - Infrastructure Overhead (Avg ns):");
        Console.WriteLine(
            "┌─────────────────────────────────────┬──────────┬──────────┬──────────┐"
        );
        Console.WriteLine(
            "│ Test Case                           │ Pico(ns) │ MsDI(ns) │ Speedup  │"
        );
        Console.WriteLine(
            "├─────────────────────────────────────┼──────────┼──────────┼──────────┤"
        );

        foreach (var r in infrastructureResults)
        {
            Console.WriteLine(
                $"│ {r.Scenario, -35} │{r.Pico.Avg, 9:F1} │{r.MsDI.Avg, 9:F1} │ {r.Speedup, 7:F2}x │"
            );
        }

        Console.WriteLine(
            "└─────────────────────────────────────┴──────────┴──────────┴──────────┘"
        );

        Console.WriteLine();
        Console.WriteLine("▶ Part 3 Results - Resolution Scenarios (Avg ns):");
        Console.WriteLine(
            "┌─────────────────────────────────────┬──────────┬──────────┬──────────┐"
        );
        Console.WriteLine(
            "│ Test Case                           │ Pico(ns) │ MsDI(ns) │ Speedup  │"
        );
        Console.WriteLine(
            "├─────────────────────────────────────┼──────────┼──────────┼──────────┤"
        );

        foreach (var r in scenarioResults)
        {
            var testName = $"{r.Scenario} × {r.Lifetime}";
            Console.WriteLine(
                $"│ {testName, -35} │{r.Pico.Avg, 9:F1} │{r.MsDI.Avg, 9:F1} │ {r.Speedup, 7:F2}x │"
            );
        }

        Console.WriteLine(
            "└─────────────────────────────────────┴──────────┴──────────┴──────────┘"
        );
    }
}

#endregion
