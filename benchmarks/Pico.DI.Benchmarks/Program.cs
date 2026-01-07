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
        // For resolution tests, pre-build containers
        var needsDeep = scenario == TestScenario.DeepDependencyChain;
        if (
            scenario
            is TestScenario.SingleResolution
                or TestScenario.MultipleResolutions
                or TestScenario.DeepDependencyChain
        )
        {
            EnsureContainersBuilt(lifetime, needsDeep);
        }

        // Warmup
        for (var i = 0; i < _warmupIterations; i++)
            ExecuteScenario(containerType, scenario, lifetime);

        // Sampled timing: run N batches and compute distribution over ns/op.
        // This provides meaningful p50/p90/p95/p99 per test case.
        var nsPerOpSamples = new double[_samples];
        var totalNs = 0d;
        ulong totalCpuCycles = 0;
        IReadOnlyList<GenCount>? lastGcDeltas = null;

        for (var s = 0; s < _samples; s++)
        {
            var summaryName = $"{containerType}/{scenario}/{lifetime}/s{s + 1}";
            var summary = Runner.Time(
                summaryName,
                iterationsPerSample,
                () => ExecuteScenario(containerType, scenario, lifetime)
            );

            totalNs += summary.ElapsedNanoseconds;
            totalCpuCycles += summary.CpuCycle;
            lastGcDeltas = summary.GenCounts;

            nsPerOpSamples[s] = summary.ElapsedNanoseconds / iterationsPerSample;
        }

        Array.Sort(nsPerOpSamples);
        var avgNs = nsPerOpSamples.Average();

        return new BenchmarkResult(
            containerType,
            scenario,
            lifetime,
            _samples,
            iterationsPerSample,
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
            using var c = new SvcContainer();
            RegisterPicoDI(c, lifetime);
            using var scope = c.CreateScope();
        }
        else
        {
            var services = new ServiceCollection();
            RegisterMsDI(services, lifetime);
            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
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
        BenchmarkRunner.Configure(
            warmupIterations: cfg.Warmup,
            samples: cfg.Samples,
            multipleResolutionsInnerLoop: cfg.MultipleResolutionsInnerLoop
        );

        Console.WriteLine(
            "╔══════════════════════════════════════════════════════════════════════════════╗"
        );
        Console.WriteLine(
            "║           Pico.DI vs Microsoft.DI - Code Runner Comparison Benchmark         ║"
        );
        Console.WriteLine(
            "╚══════════════════════════════════════════════════════════════════════════════╝"
        );
        Console.WriteLine();

        Runner.Initialize();

        var results = new List<BenchmarkResult>();

        // Matrix: Container × Scenario × Lifetime
        var containers = new[] { ContainerType.PicoDI, ContainerType.MsDI };
        var scenarios = new[]
        {
            TestScenario.ContainerSetup,
            TestScenario.ScopeCreation,
            TestScenario.SingleResolution,
            TestScenario.MultipleResolutions,
            TestScenario.DeepDependencyChain
        };
        var lifetimes = new[]
        {
            LifetimeType.Transient,
            LifetimeType.Scoped,
            LifetimeType.Singleton
        };

        var totalTests = containers.Length * scenarios.Length * lifetimes.Length;
        var currentTest = 0;

        foreach (var scenario in scenarios)
        {
            Console.WriteLine($"▶ Running: {scenario}");

            foreach (var lifetime in lifetimes)
            {
                // Cleanup between lifetime changes to ensure fair test
                BenchmarkRunner.CleanupContainers();

                foreach (var container in containers)
                {
                    currentTest++;
                    Console.Write(
                        $"  [{currentTest}/{totalTests}] {container, -8} × {lifetime, -10}... "
                    );

                    var result = BenchmarkRunner.Run(
                        container,
                        scenario,
                        lifetime,
                        iterationsPerSample: cfg.IterationsPerSample
                    );
                    results.Add(result);

                    var cyclesPerOp =
                        result.CpuCycles == 0
                            ? "n/a"
                            : (result.CpuCycles / (double)result.IterationsPerSample).ToString(
                                "N0"
                            );

                    Console.WriteLine(
                        $"avg {result.AvgNs, 8:F1} ns/op | p50 {result.P50Ns, 8:F1} ns/op | cycles/op: {cyclesPerOp, 7} | GCΔ: {FormatGcDeltas(result.GcGenDeltas)}"
                    );
                }
            }
            Console.WriteLine();
        }

        // Cleanup
        BenchmarkRunner.CleanupContainers();

        // Print comparison tables
        PrintComparisonReport(results);
    }

    private static void PrintComparisonReport(List<BenchmarkResult> results)
    {
        Console.WriteLine(
            "╔══════════════════════════════════════════════════════════════════════════════╗"
        );
        Console.WriteLine(
            "║                              COMPARISON REPORT                               ║"
        );
        Console.WriteLine(
            "╚══════════════════════════════════════════════════════════════════════════════╝"
        );
        Console.WriteLine();

        const int caseW = 20;
        const int avgW = 11;
        const int p50W = 11;
        const int p90W = 11;
        const int p95W = 11;
        const int p99W = 11;
        const int cpuW = 11;
        const int gcW = 14;
        const int speedW = 8;

        var segments = new[]
        {
            caseW + 2,
            avgW + 2,
            p50W + 2,
            p90W + 2,
            p95W + 2,
            p99W + 2,
            cpuW + 2,
            gcW + 2,
            speedW + 2
        };

        static string Truncate(string value, int width)
        {
            if (value.Length <= width)
                return value;
            if (width <= 3)
                return value[..width];
            return value[..(width - 3)] + "...";
        }

        var byScenario = results.GroupBy(r => r.Scenario).OrderBy(g => g.Key).ToList();

        foreach (var scenarioGroup in byScenario)
        {
            var first = scenarioGroup.First();
            Console.WriteLine(
                $"▶ Scenario: {scenarioGroup.Key} (samples={first.Samples}, iterationsPerSample={first.IterationsPerSample})"
            );

            Console.WriteLine(Line('┌', '┬', '┐', segments));
            Console.WriteLine(
                $"│ {"Test case", -caseW} │ {"Avg(ns/op)", avgW} │ {"P50(ns/op)", p50W} │ {"P90(ns/op)", p90W} │ {"P95(ns/op)", p95W} │ {"P99(ns/op)", p99W} │ {"CPU(cy)", -cpuW} │ {"GC", -gcW} │ {"Pico x", -speedW} │"
            );
            Console.WriteLine(Line('├', '┼', '┤', segments));

            foreach (
                var lifetime in new[]
                {
                    LifetimeType.Transient,
                    LifetimeType.Scoped,
                    LifetimeType.Singleton
                }
            )
            {
                var pico = scenarioGroup.First(
                    r => r.Lifetime == lifetime && r.Container == ContainerType.PicoDI
                );
                var ms = scenarioGroup.First(
                    r => r.Lifetime == lifetime && r.Container == ContainerType.MsDI
                );

                var picoTime = pico.AvgNs;
                var msTime = ms.AvgNs;
                var speedup = picoTime <= 0 ? "n/a" : (msTime / picoTime).ToString("0.00") + "x";

                var picoCpu = CpuCyclesPerOpString(pico);
                var msCpu = CpuCyclesPerOpString(ms);

                var picoGc = Truncate(FormatGcDeltas(pico.GcGenDeltas), gcW);
                var msGc = Truncate(FormatGcDeltas(ms.GcGenDeltas), gcW);

                var picoCase = Truncate($"{ContainerType.PicoDI} × {lifetime}", caseW);
                var msCase = Truncate($"{ContainerType.MsDI} × {lifetime}", caseW);

                Console.WriteLine(
                    $"│ {picoCase, -caseW} │ {pico.AvgNs, avgW:F1} │ {pico.P50Ns, p50W:F1} │ {pico.P90Ns, p90W:F1} │ {pico.P95Ns, p95W:F1} │ {pico.P99Ns, p99W:F1} │ {Truncate(picoCpu, cpuW), -cpuW} │ {picoGc, -gcW} │ {Truncate(speedup, speedW), -speedW} │"
                );
                Console.WriteLine(
                    $"│ {msCase, -caseW} │ {ms.AvgNs, avgW:F1} │ {ms.P50Ns, p50W:F1} │ {ms.P90Ns, p90W:F1} │ {ms.P95Ns, p95W:F1} │ {ms.P99Ns, p99W:F1} │ {Truncate(msCpu, cpuW), -cpuW} │ {msGc, -gcW} │ {"", -speedW} │"
                );
            }

            Console.WriteLine(Line('└', '┴', '┘', segments));

            PrintScenarioTotalsComparison(scenarioGroup.ToList());

            Console.WriteLine();
        }

        PrintTotalsComparison(results);

        // Summary (wins count)
        PrintSummary(results);
        return;

        static string Line(char left, char mid, char right, int[] segs)
        {
            var parts = segs.Select(s => new string('─', s));
            return left + string.Join(mid, parts) + right;
        }
    }

    private sealed record TotalsAgg(
        double AvgNs,
        double P50Ns,
        double P90Ns,
        double P95Ns,
        double P99Ns,
        double? CpuCyclesPerOp,
        Dictionary<int, int> GcTotals
    );

    private static TotalsAgg ComputeAgg(List<BenchmarkResult> cases)
    {
        var cpu = cases.All(r => r.CpuCycles == 0)
            ? (double?)null
            : cases.Average(r => r.CpuCycles / (double)r.IterationsPerSample);

        return new TotalsAgg(
            AvgNs: cases.Average(r => r.AvgNs),
            P50Ns: cases.Average(r => r.P50Ns),
            P90Ns: cases.Average(r => r.P90Ns),
            P95Ns: cases.Average(r => r.P95Ns),
            P99Ns: cases.Average(r => r.P99Ns),
            CpuCyclesPerOp: cpu,
            GcTotals: SumGcAllGens(cases)
        );
    }

    private static double? Ratio(double numerator, double denominator)
    {
        if (double.IsNaN(numerator) || double.IsNaN(denominator))
            return null;
        if (double.IsInfinity(numerator) || double.IsInfinity(denominator))
            return null;
        if (denominator <= 0)
            return null;
        return numerator / denominator;
    }

    private static void PrintTotalsBlock(string label, TotalsAgg pico, TotalsAgg ms)
    {
        var picoGcSum = pico.GcTotals.Values.Sum();
        var msGcSum = ms.GcTotals.Values.Sum();

        const int timeW = 9;
        const int cpuW = 11;
        const int gcW = 14;

        static string Truncate(string value, int width)
        {
            if (value.Length <= width)
                return value;
            if (width <= 3)
                return value[..width];
            return value[..(width - 3)] + "...";
        }

        static string FTime(double v) => v.ToString("0.0");

        static string FormatGcRatio(int msSum, int picoSum)
        {
            if (picoSum == 0)
                return msSum == 0 ? "1.00x" : "inf";
            return (msSum / (double)picoSum).ToString("0.00") + "x";
        }

        var picoCpu = FormatNumber(pico.CpuCyclesPerOp, "N0");
        var msCpu = FormatNumber(ms.CpuCyclesPerOp, "N0");
        var picoGc = Truncate(FormatGcTotals(pico.GcTotals), gcW);
        var msGc = Truncate(FormatGcTotals(ms.GcTotals), gcW);

        var cpuRatio =
            pico.CpuCyclesPerOp is null || ms.CpuCyclesPerOp is null
                ? null
                : Ratio(ms.CpuCyclesPerOp.Value, pico.CpuCyclesPerOp.Value);

        Console.WriteLine($"{label}: (avg across cases)");
        Console.WriteLine(
            $"  {"Pico", -6} | Avg {FTime(pico.AvgNs), timeW} | P50 {FTime(pico.P50Ns), timeW} | P90 {FTime(pico.P90Ns), timeW} | P95 {FTime(pico.P95Ns), timeW} | P99 {FTime(pico.P99Ns), timeW} | CPU {picoCpu, cpuW} | GC {picoGc, -gcW}"
        );
        Console.WriteLine(
            $"  {"Ms", -6} | Avg {FTime(ms.AvgNs), timeW} | P50 {FTime(ms.P50Ns), timeW} | P90 {FTime(ms.P90Ns), timeW} | P95 {FTime(ms.P95Ns), timeW} | P99 {FTime(ms.P99Ns), timeW} | CPU {msCpu, cpuW} | GC {msGc, -gcW}"
        );
        Console.WriteLine(
            $"  {"Pico x", -6} | Avg {FormatRatio(Ratio(ms.AvgNs, pico.AvgNs)), timeW} | P50 {FormatRatio(Ratio(ms.P50Ns, pico.P50Ns)), timeW} | P90 {FormatRatio(Ratio(ms.P90Ns, pico.P90Ns)), timeW} | P95 {FormatRatio(Ratio(ms.P95Ns, pico.P95Ns)), timeW} | P99 {FormatRatio(Ratio(ms.P99Ns, pico.P99Ns)), timeW} | CPU {FormatRatio(cpuRatio), cpuW} | GC {FormatGcRatio(msGcSum, picoGcSum), gcW}"
        );
    }

    private static void PrintScenarioTotalsComparison(List<BenchmarkResult> scenarioResults)
    {
        var picoCases = scenarioResults.Where(r => r.Container == ContainerType.PicoDI).ToList();
        var msCases = scenarioResults.Where(r => r.Container == ContainerType.MsDI).ToList();

        var picoAgg = ComputeAgg(picoCases);
        var msAgg = ComputeAgg(msCases);

        Console.WriteLine();
        PrintTotalsBlock("Totals", picoAgg, msAgg);
    }

    private static void PrintTotalsComparison(List<BenchmarkResult> results)
    {
        Console.WriteLine(
            "╔══════════════════════════════════════════════════════════════════════════════╗"
        );
        Console.WriteLine(
            "║                                   TOTALS                                     ║"
        );
        Console.WriteLine(
            "╚══════════════════════════════════════════════════════════════════════════════╝"
        );
        Console.WriteLine();

        var picoCases = results.Where(r => r.Container == ContainerType.PicoDI).ToList();
        var msCases = results.Where(r => r.Container == ContainerType.MsDI).ToList();

        var picoAgg = ComputeAgg(picoCases);
        var msAgg = ComputeAgg(msCases);

        PrintTotalsBlock("Totals", picoAgg, msAgg);
        Console.WriteLine();
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

    private static string CpuCyclesPerOpString(BenchmarkResult r)
    {
        return r.CpuCycles == 0
            ? "n/a"
            : (r.CpuCycles / (double)r.IterationsPerSample).ToString("N0");
    }

    private static Dictionary<int, int> SumGcAllGens(List<BenchmarkResult> cases)
    {
        var totals = new Dictionary<int, int>();
        foreach (var d in cases.SelectMany(r => r.GcGenDeltas))
        {
            totals.TryGetValue(d.Gen, out var existing);
            totals[d.Gen] = existing + d.Count;
        }

        return totals;
    }

    private static string FormatGcTotals(Dictionary<int, int> totals)
    {
        if (totals.Count == 0)
            return "0";

        var parts = totals
            .Where(kvp => kvp.Value != 0)
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => $"Gen{kvp.Key}+{kvp.Value}")
            .ToArray();

        return parts.Length == 0 ? "0" : string.Join(" ", parts);
    }

    private static string FormatNumber(double? value, string format)
    {
        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            return "n/a";
        return value.Value.ToString(format);
    }

    private static string FormatRatio(double? value)
    {
        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            return "n/a";
        return value.Value.ToString("0.00") + "x";
    }

    private static void PrintSummary(List<BenchmarkResult> results)
    {
        var picoWins = 0;
        var msWins = 0;

        var lifetimes = new[]
        {
            LifetimeType.Transient,
            LifetimeType.Scoped,
            LifetimeType.Singleton
        };
        var scenarios = results.Select(r => r.Scenario).Distinct();

        foreach (var scenario in scenarios)
        {
            foreach (var lifetime in lifetimes)
            {
                var pico = results.First(
                    r =>
                        r.Scenario == scenario
                        && r.Lifetime == lifetime
                        && r.Container == ContainerType.PicoDI
                );
                var ms = results.First(
                    r =>
                        r.Scenario == scenario
                        && r.Lifetime == lifetime
                        && r.Container == ContainerType.MsDI
                );

                if (pico.AvgNs < ms.AvgNs)
                    picoWins++;
                else
                    msWins++;
            }
        }

        const int innerW = 78;

        Console.WriteLine($"╔{new string('═', innerW)}╗");
        WriteRow(Center("SUMMARY", innerW), innerW);
        Console.WriteLine($"╠{new string('═', innerW)}╣");

        var total = picoWins + msWins;
        WriteRow($"  Pico.DI wins: {picoWins, 3} / {total, 3}", innerW);
        WriteRow($"  Ms.DI wins:   {msWins, 3} / {total, 3}", innerW);

        Console.WriteLine($"╚{new string('═', innerW)}╝");
        return;

        static void WriteRow(string content, int width) =>
            Console.WriteLine($"║{content.PadRight(width)}║");

        static string Center(string text, int width)
        {
            if (text.Length >= width)
                return text[..width];
            var left = (width - text.Length) / 2;
            return new string(' ', left) + text + new string(' ', width - left - text.Length);
        }
    }

    private static string FormatGcDeltas(IReadOnlyList<GenCount> deltas, bool verbose = false)
    {
        if (deltas.Count == 0)
            return "0";

        var prefix = verbose ? "Gen" : "G";
        var parts = deltas
            .Where(d => d.Count != 0)
            .Select(d => $"{prefix}{d.Gen}+{d.Count}")
            .ToArray();

        return parts.Length == 0 ? "0" : string.Join(" ", parts);
    }

    private static string FormatGcDeltasAllGens(IReadOnlyList<GenCount> deltas)
    {
        return deltas.Count == 0
            ? "0"
            : string.Join(" ", deltas.Select(d => $"G{d.Gen}:{d.Count}")); // Include all generations, even if count is 0.
    }
}

#endregion
