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
    int Iterations,
    double TotalMs,
    double AvgNs,
    double MinNs,
    double MaxNs,
    ulong CpuCycles,
    IReadOnlyList<GenCount> GcGenDeltas
);

public static class BenchmarkRunner
{
    private const int WarmupIterations = 1000;
    private const int DefaultIterations = 10000;

    // Pre-built containers for resolution tests (avoid container creation overhead)
    private static SvcContainer? _picoContainer;
    private static ServiceProvider? _msProvider;
    private static LifetimeType _currentLifetime;
    private static bool _isDeep;

    public static BenchmarkResult Run(
        ContainerType containerType,
        TestScenario scenario,
        LifetimeType lifetime,
        int iterations = DefaultIterations
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
        for (var i = 0; i < WarmupIterations; i++)
            ExecuteScenario(containerType, scenario, lifetime);

        // Batch timing via Runner.Time (AOT-friendly, captures GC deltas + CPU cycles)
        var summaryName = $"{containerType}/{scenario}/{lifetime}";
        var summary = Runner.Time(
            summaryName,
            iterations,
            () => ExecuteScenario(containerType, scenario, lifetime)
        );

        var totalNs = summary.ElapsedNanoseconds;
        var avgNs = totalNs / iterations;

        // Runner.Time measures the whole batch; Min/Max are not sampled per-iteration here.
        return new BenchmarkResult(
            containerType,
            scenario,
            lifetime,
            iterations,
            totalNs / 1_000_000d,
            avgNs,
            avgNs,
            avgNs,
            summary.CpuCycle,
            summary.GenCounts
        );
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
        _picoContainer = new SvcContainer(autoConfigureFromGenerator: false);
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
            using var c = new SvcContainer(autoConfigureFromGenerator: false);
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
            using var c = new SvcContainer(autoConfigureFromGenerator: false);
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
            for (var i = 0; i < 100; i++)
                _ = scope.GetService<IService>();
        }
        else
        {
            using var scope = _msProvider!.CreateScope();
            for (var i = 0; i < 100; i++)
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
        var lt = lifetime switch
        {
            LifetimeType.Transient => SvcLifetime.Transient,
            LifetimeType.Scoped => SvcLifetime.Scoped,
            LifetimeType.Singleton => SvcLifetime.Singleton,
            _ => throw new ArgumentOutOfRangeException()
        };

        // Use factory-based registration for fair comparison with runtime lifetime
        c.Register<ILogger>(_ => new ConsoleLogger(), lt);
        c.Register<IRepository>(_ => new Repository(), lt);
        c.Register<IService>(
            sp => new ServiceC(sp.GetService<ILogger>(), sp.GetService<IRepository>()),
            lt
        );
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
        var lt = lifetime switch
        {
            LifetimeType.Transient => SvcLifetime.Transient,
            LifetimeType.Scoped => SvcLifetime.Scoped,
            LifetimeType.Singleton => SvcLifetime.Singleton,
            _ => throw new ArgumentOutOfRangeException()
        };

        // Use factory-based registration for deep dependency chain
        c.Register<ILevel1>(_ => new Level1(), lt);
        c.Register<ILevel2>(sp => new Level2(sp.GetService<ILevel1>()), lt);
        c.Register<ILevel3>(sp => new Level3(sp.GetService<ILevel2>()), lt);
        c.Register<ILevel4>(sp => new Level4(sp.GetService<ILevel3>()), lt);
        c.Register<ILevel5>(sp => new Level5(sp.GetService<ILevel4>()), lt);
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
    public static void Main()
    {
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

                    var result = BenchmarkRunner.Run(container, scenario, lifetime);
                    results.Add(result);

                    var cyclesPerOp =
                        result.CpuCycles == 0
                            ? "n/a"
                            : (result.CpuCycles / (double)result.Iterations).ToString("N0");

                    Console.WriteLine(
                        $"{result.AvgNs, 10:F1} ns/op | cycles/op: {cyclesPerOp, 7} | GCΔ: {FormatGcDeltas(result.GcGenDeltas)}"
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
        const int timeW = 11;
        const int cpuW = 11;
        const int gcW = 14;
        const int speedW = 8;

        var segments = new[] { caseW + 2, timeW + 2, cpuW + 2, gcW + 2, speedW + 2 };

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
            Console.WriteLine($"▶ Scenario: {scenarioGroup.Key}");

            Console.WriteLine(Line('┌', '┬', '┐', segments));
            Console.WriteLine(
                $"│ {"Test case", -caseW} │ {"Time(ns)", -timeW} │ {"CPU(cy)", -cpuW} │ {"GC", -gcW} │ {"Pico x", -speedW} │"
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

                var picoGc = Truncate(FormatGcDeltasAllGens(pico.GcGenDeltas), gcW);
                var msGc = Truncate(FormatGcDeltasAllGens(ms.GcGenDeltas), gcW);

                var picoCase = Truncate($"{ContainerType.PicoDI} × {lifetime}", caseW);
                var msCase = Truncate($"{ContainerType.MsDI} × {lifetime}", caseW);

                Console.WriteLine(
                    $"│ {picoCase, -caseW} │ {picoTime, timeW:F1} │ {Truncate(picoCpu, cpuW), -cpuW} │ {picoGc, -gcW} │ {Truncate(speedup, speedW), -speedW} │"
                );
                Console.WriteLine(
                    $"│ {msCase, -caseW} │ {msTime, timeW:F1} │ {Truncate(msCpu, cpuW), -cpuW} │ {msGc, -gcW} │ {"", -speedW} │"
                );
            }

            Console.WriteLine(Line('└', '┴', '┘', segments));
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

        var picoAvgNs = picoCases.Average(r => r.AvgNs);
        var msAvgNs = msCases.Average(r => r.AvgNs);
        var timeSpeedup = picoAvgNs <= 0 ? double.NaN : msAvgNs / picoAvgNs;

        var picoAvgCy = picoCases.All(r => r.CpuCycles == 0)
            ? (double?)null
            : picoCases.Average(r => r.CpuCycles / (double)r.Iterations);
        var msAvgCy = msCases.All(r => r.CpuCycles == 0)
            ? (double?)null
            : msCases.Average(r => r.CpuCycles / (double)r.Iterations);
        var cpuSpeedup =
            picoAvgCy is null || msAvgCy is null || picoAvgCy <= 0
                ? (double?)null
                : msAvgCy / picoAvgCy;

        var picoGcTotals = SumGcAllGens(picoCases);
        var msGcTotals = SumGcAllGens(msCases);
        var picoGcSum = picoGcTotals.Values.Sum();
        var msGcSum = msGcTotals.Values.Sum();
        var gcReduction = picoGcSum == 0 ? (double?)null : msGcSum / picoGcSum;

        Console.WriteLine(
            $"Time (avg ns/op): Pico {picoAvgNs:F1} | Ms {msAvgNs:F1} | Pico x {timeSpeedup:0.00}x"
        );
        Console.WriteLine(
            $"CPU  (avg cy/op): Pico {FormatNumber(picoAvgCy, "N0")} | Ms {FormatNumber(msAvgCy, "N0")} | Pico x {FormatRatio(cpuSpeedup)}"
        );
        Console.WriteLine(
            $"GC   (sum):       Pico {FormatGcTotals(picoGcTotals)} | Ms {FormatGcTotals(msGcTotals)} | Pico x {FormatRatio(gcReduction)}"
        );
        Console.WriteLine();
    }

    private static string CpuCyclesPerOpString(BenchmarkResult r)
    {
        return r.CpuCycles == 0 ? "n/a" : (r.CpuCycles / (double)r.Iterations).ToString("N0");
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
            .OrderBy(kvp => kvp.Key)
            .Where(kvp => kvp.Value != 0)
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

        static string Center(string text, int width)
        {
            if (text.Length >= width)
                return text[..width];
            var left = (width - text.Length) / 2;
            return new string(' ', left) + text + new string(' ', width - left - text.Length);
        }

        static void WriteRow(string content, int width) =>
            Console.WriteLine($"║{content.PadRight(width)}║");

        Console.WriteLine($"╔{new string('═', innerW)}╗");
        WriteRow(Center("SUMMARY", innerW), innerW);
        Console.WriteLine($"╠{new string('═', innerW)}╣");

        var total = picoWins + msWins;
        WriteRow($"  Pico.DI wins: {picoWins, 3} / {total, 3}", innerW);
        WriteRow($"  Ms.DI wins:   {msWins, 3} / {total, 3}", innerW);

        Console.WriteLine($"╚{new string('═', innerW)}╝");
    }

    private static string FormatGcDeltas(IReadOnlyList<GenCount> deltas, bool verbose = false)
    {
        if (deltas.Count == 0)
            return "n/a";

        var prefix = verbose ? "Gen" : "G";
        var parts = deltas
            .Where(d => d.Count != 0)
            .Select(d => $"{prefix}{d.Gen}+{d.Count}")
            .ToArray();

        return parts.Length == 0 ? "0" : string.Join(" ", parts);
    }

    private static string FormatGcDeltasAllGens(IReadOnlyList<GenCount> deltas)
    {
        if (deltas.Count == 0)
            return "n/a";

        // Include all generations, even if count is 0.
        return string.Join(" ", deltas.Select(d => $"G{d.Gen}:{d.Count}"));
    }
}

#endregion
