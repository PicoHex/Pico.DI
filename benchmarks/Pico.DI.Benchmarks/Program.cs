using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Pico.DI;
using Pico.DI.Abs;

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
    double MaxNs
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

        // Collect individual timings
        var timings = new double[iterations];
        var sw = new Stopwatch();

        for (var i = 0; i < iterations; i++)
        {
            sw.Restart();
            ExecuteScenario(containerType, scenario, lifetime);
            sw.Stop();
            timings[i] = sw.Elapsed.TotalNanoseconds;
        }

        return new BenchmarkResult(
            containerType,
            scenario,
            lifetime,
            iterations,
            timings.Sum() / 1_000_000,
            timings.Average(),
            timings.Min(),
            timings.Max()
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
                ExecuteSingleResolution(container, lifetime);
                break;
            case TestScenario.MultipleResolutions:
                ExecuteMultipleResolutions(container, lifetime);
                break;
            case TestScenario.DeepDependencyChain:
                ExecuteDeepDependencyChain(container, lifetime);
                break;
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

    private static void ExecuteSingleResolution(ContainerType container, LifetimeType lifetime)
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

    private static void ExecuteMultipleResolutions(ContainerType container, LifetimeType lifetime)
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

    private static void ExecuteDeepDependencyChain(ContainerType container, LifetimeType lifetime)
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
            "║            Pico.DI vs Microsoft.DI - Stopwatch Comparison Benchmark          ║"
        );
        Console.WriteLine(
            "╚══════════════════════════════════════════════════════════════════════════════╝"
        );
        Console.WriteLine();

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

                    Console.WriteLine($"{result.AvgNs, 10:F1} ns/op");
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

        var scenarios = results.Select(r => r.Scenario).Distinct();

        foreach (var scenario in scenarios)
        {
            Console.WriteLine(
                $"┌─────────────────────────────────────────────────────────────────────────────┐"
            );
            Console.WriteLine($"│ {scenario, -75} │");
            Console.WriteLine(
                $"├────────────┬────────────┬────────────┬────────────┬────────────┬───────────┤"
            );
            Console.WriteLine(
                $"│ {"Lifetime", -10} │ {"Pico.DI", -10} │ {"Ms.DI", -10} │ {"Diff", -10} │ {"Ratio", -10} │ {"Winner", -9} │"
            );
            Console.WriteLine(
                $"├────────────┼────────────┼────────────┼────────────┼────────────┼───────────┤"
            );

            var lifetimes = new[]
            {
                LifetimeType.Transient,
                LifetimeType.Scoped,
                LifetimeType.Singleton
            };

            foreach (var lifetime in lifetimes)
            {
                var picoResult = results.First(
                    r =>
                        r.Scenario == scenario
                        && r.Lifetime == lifetime
                        && r.Container == ContainerType.PicoDI
                );

                var msResult = results.First(
                    r =>
                        r.Scenario == scenario
                        && r.Lifetime == lifetime
                        && r.Container == ContainerType.MsDI
                );

                var diff = picoResult.AvgNs - msResult.AvgNs;
                var ratio = picoResult.AvgNs / msResult.AvgNs;
                var winner = picoResult.AvgNs < msResult.AvgNs ? "Pico.DI" : "Ms.DI";
                var winnerColor = winner == "Pico.DI" ? "✓" : " ";

                Console.WriteLine(
                    $"│ {lifetime, -10} │ {picoResult.AvgNs, 8:F1} ns │ {msResult.AvgNs, 8:F1} ns │ {diff, +8:F1} ns │ {ratio, 9:F2}x │ {winnerColor}{winner, -8} │"
                );
            }

            Console.WriteLine(
                $"└────────────┴────────────┴────────────┴────────────┴────────────┴───────────┘"
            );
            Console.WriteLine();
        }

        // Summary
        PrintSummary(results);
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

        Console.WriteLine(
            "╔══════════════════════════════════════════════════════════════════════════════╗"
        );
        Console.WriteLine(
            "║                                  SUMMARY                                     ║"
        );
        Console.WriteLine(
            "╠══════════════════════════════════════════════════════════════════════════════╣"
        );
        Console.WriteLine(
            $"║  Pico.DI wins: {picoWins, 3} / {picoWins + msWins, -3}                                                        ║"
        );
        Console.WriteLine(
            $"║  Ms.DI wins:   {msWins, 3} / {picoWins + msWins, -3}                                                        ║"
        );
        Console.WriteLine(
            "╚══════════════════════════════════════════════════════════════════════════════╝"
        );
    }
}

#endregion
