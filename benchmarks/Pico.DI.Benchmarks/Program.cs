using Pico.Bench;
using Pico.Bench.Formatters;

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
/// Service complexity categories for benchmarking
/// </summary>
public enum ServiceComplexity
{
    NoDependency,
    SingleDependency,
    MultipleDependencies,
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

#region Static Factories

/// <summary>
/// Pre-compiled static factories - equivalent to what Source Generator produces.
/// </summary>
public static class Factories
{
    public static readonly Func<ISvcScope, ISimpleService> SimpleService =
        static _ => new SimpleService();
    public static readonly Func<ISvcScope, ILogger> Logger = static _ => new ConsoleLogger();
    public static readonly Func<ISvcScope, IServiceWithDep> ServiceWithDep =
        static s => new ServiceWithDep(s.GetService<ILogger>());
    public static readonly Func<ISvcScope, IRepository> Repository = static _ => new Repository();
    public static readonly Func<ISvcScope, IServiceWithMultipleDeps> ServiceWithMultipleDeps =
        static s => new ServiceWithMultipleDeps(
            s.GetService<ILogger>(),
            s.GetService<IRepository>()
        );
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
                RegisterMs<ISimpleService>(services, lifetime, _ => new SimpleService());
                break;
            case ServiceComplexity.SingleDependency:
                RegisterMs<ILogger>(services, lifetime, _ => new ConsoleLogger());
                RegisterMs<IServiceWithDep>(
                    services,
                    lifetime,
                    sp => new ServiceWithDep(sp.GetRequiredService<ILogger>())
                );
                break;
            case ServiceComplexity.MultipleDependencies:
                RegisterMs<ILogger>(services, lifetime, _ => new ConsoleLogger());
                RegisterMs<IRepository>(services, lifetime, _ => new Repository());
                RegisterMs<IServiceWithMultipleDeps>(
                    services,
                    lifetime,
                    sp => new ServiceWithMultipleDeps(
                        sp.GetRequiredService<ILogger>(),
                        sp.GetRequiredService<IRepository>()
                    )
                );
                break;
            case ServiceComplexity.DeepChain:
                RegisterMs<ILevel1>(services, lifetime, _ => new Level1());
                RegisterMs<ILevel2>(
                    services,
                    lifetime,
                    sp => new Level2(sp.GetRequiredService<ILevel1>())
                );
                RegisterMs<ILevel3>(
                    services,
                    lifetime,
                    sp => new Level3(sp.GetRequiredService<ILevel2>())
                );
                RegisterMs<ILevel4>(
                    services,
                    lifetime,
                    sp => new Level4(sp.GetRequiredService<ILevel3>())
                );
                RegisterMs<ILevel5>(
                    services,
                    lifetime,
                    sp => new Level5(sp.GetRequiredService<ILevel4>())
                );
                break;
        }

        return services.BuildServiceProvider();
    }

    private static void RegisterMs<T>(
        ServiceCollection services,
        Lifetime lifetime,
        Func<IServiceProvider, T> factory
    )
        where T : class
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

#region Main Program

public static class Program
{
    private static readonly BenchmarkConfig Config = BenchmarkConfig.Default;

    public static void Main(string[] args)
    {
        Pico.Bench.Runner.Initialize();

        var env = new EnvironmentInfo();
        var startTime = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();

        Console.WriteLine();
        Console.WriteLine(
            "╔═══════════════════════════════════════════════════════════════════════════════════════════════════════════════╗"
        );
        Console.WriteLine(
            "║                          Pico.DI vs Microsoft.DI - Comprehensive Benchmark                                    ║"
        );
        Console.WriteLine(
            "╚═══════════════════════════════════════════════════════════════════════════════════════════════════════════════╝"
        );
        Console.WriteLine();
        Console.WriteLine($"Environment: {env}");
        Console.WriteLine(
            $"Config: {Config.SampleCount} samples × {Config.IterationsPerSample} iterations"
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

        var comparisons = new List<ComparisonResult>();

        // Part 1: Service Resolution by Complexity
        Console.WriteLine(
            "═══════════════════════════════════════════════════════════════════════════════════════════════════════════════"
        );
        Console.WriteLine(
            "  PART 1: Service Resolution by Complexity (GetService<T>() within a scope)"
        );
        Console.WriteLine(
            "═══════════════════════════════════════════════════════════════════════════════════════════════════════════════"
        );
        Console.WriteLine();

        foreach (var complexity in complexities)
        {
            Console.WriteLine($"▶ Testing: {complexity}");

            foreach (var lifetime in lifetimes)
            {
                Console.Write($"  Running {complexity} × {lifetime}...".PadRight(60));
                var comparison = RunResolutionBenchmark(complexity, lifetime);
                comparisons.Add(comparison);
                Console.WriteLine(
                    $" {comparison.Candidate.Statistics.Avg, 8:F1} ns vs {comparison.Baseline.Statistics.Avg, 8:F1} ns = {comparison.Speedup:F2}x"
                );
            }
            Console.WriteLine();
        }

        // Part 2: Infrastructure Overhead
        Console.WriteLine(
            "═══════════════════════════════════════════════════════════════════════════════════════════════════════════════"
        );
        Console.WriteLine("  PART 2: Infrastructure Overhead");
        Console.WriteLine(
            "═══════════════════════════════════════════════════════════════════════════════════════════════════════════════"
        );
        Console.WriteLine();

        Console.Write("  Running ContainerSetup...".PadRight(60));
        var containerSetup = RunContainerSetupBenchmark();
        comparisons.Add(containerSetup);
        Console.WriteLine(
            $" {containerSetup.Candidate.Statistics.Avg, 8:F1} ns vs {containerSetup.Baseline.Statistics.Avg, 8:F1} ns = {containerSetup.Speedup:F2}x"
        );

        Console.Write("  Running ScopeCreation...".PadRight(60));
        var scopeCreation = RunScopeCreationBenchmark();
        comparisons.Add(scopeCreation);
        Console.WriteLine(
            $" {scopeCreation.Candidate.Statistics.Avg, 8:F1} ns vs {scopeCreation.Baseline.Statistics.Avg, 8:F1} ns = {scopeCreation.Speedup:F2}x"
        );
        Console.WriteLine();

        // Part 3: Resolution Scenarios
        Console.WriteLine(
            "═══════════════════════════════════════════════════════════════════════════════════════════════════════════════"
        );
        Console.WriteLine("  PART 3: Resolution Scenarios (hot path performance)");
        Console.WriteLine(
            "═══════════════════════════════════════════════════════════════════════════════════════════════════════════════"
        );
        Console.WriteLine();

        foreach (var lifetime in lifetimes)
        {
            Console.Write($"  Running SingleResolution × {lifetime}...".PadRight(60));
            var single = RunSingleResolutionBenchmark(lifetime);
            comparisons.Add(single);
            Console.WriteLine(
                $" {single.Candidate.Statistics.Avg, 8:F1} ns vs {single.Baseline.Statistics.Avg, 8:F1} ns = {single.Speedup:F2}x"
            );
        }
        Console.WriteLine();

        foreach (var lifetime in lifetimes)
        {
            Console.Write($"  Running MultipleResolutions × {lifetime}...".PadRight(60));
            var multi = RunMultipleResolutionsBenchmark(lifetime);
            comparisons.Add(multi);
            Console.WriteLine(
                $" {multi.Candidate.Statistics.Avg, 8:F1} ns vs {multi.Baseline.Statistics.Avg, 8:F1} ns = {multi.Speedup:F2}x"
            );
        }
        Console.WriteLine();

        sw.Stop();

        // Summary using Pico.Bench SummaryFormatter
        var summaryOptions = new SummaryOptions
        {
            CandidateLabel = "Pico.DI",
            GroupByCategory = true,
            ShowDetailedTable = true,
            ShowDuration = true
        };
        SummaryFormatter.Write(comparisons, sw.Elapsed, summaryOptions);

        // Output to files if requested
        if (args.Contains("--csv") || args.Contains("--all"))
        {
            var csvPath = "benchmark-results.csv";
            CsvFormatter.WriteToFile(csvPath, comparisons);
            Console.WriteLine($"\nCSV results saved to: {csvPath}");
        }

        // Build suite for file output
        var suite = new BenchmarkSuite
        {
            Name = "Pico.DI vs Microsoft.DI Benchmark",
            Description = "Comprehensive DI container performance comparison",
            Environment = env,
            Results = comparisons.SelectMany(c => new[] { c.Baseline, c.Candidate }).ToList(),
            Comparisons = comparisons,
            Timestamp = startTime,
            Duration = sw.Elapsed
        };

        if (args.Contains("--markdown") || args.Contains("--all"))
        {
            var mdPath = "benchmark-results.md";
            MarkdownFormatter.WriteToFile(mdPath, suite);
            Console.WriteLine($"\nMarkdown results saved to: {mdPath}");
        }

        if (args.Contains("--html") || args.Contains("--all"))
        {
            var htmlPath = "benchmark-results.html";
            HtmlFormatter.WriteToFile(htmlPath, suite);
            Console.WriteLine($"HTML results saved to: {htmlPath}");
        }
    }

    #region Benchmark Methods

    private static ComparisonResult RunResolutionBenchmark(
        ServiceComplexity complexity,
        Lifetime lifetime
    )
    {
        var name = $"{complexity} × {lifetime}";

        using var picoContainer = ContainerSetup.CreatePicoContainer(complexity, lifetime);
        using var msProvider = ContainerSetup.CreateMsContainer(complexity, lifetime);

        var (picoResolve, msResolve) = GetResolveActions(complexity);

        var picoResult = Benchmark.RunScoped(
            $"Pico/{name}",
            () => picoContainer.CreateScope(),
            scope => picoResolve(scope),
            Config
        );

        var msResult = Benchmark.RunScoped(
            $"MsDI/{name}",
            () => msProvider.CreateScope(),
            scope => msResolve(((IServiceScope)scope).ServiceProvider),
            Config
        );

        return new ComparisonResult
        {
            Name = name,
            Category = complexity.ToString(),
            Tags = new Dictionary<string, string>
            {
                ["Complexity"] = complexity.ToString(),
                ["Lifetime"] = lifetime.ToString()
            },
            Baseline = msResult,
            Candidate = picoResult
        };
    }

    private static ComparisonResult RunContainerSetupBenchmark()
    {
        var config = Config with { IterationsPerSample = Config.IterationsPerSample / 10 };

        var picoResult = Benchmark.Run(
            "Pico/ContainerSetup",
            () =>
            {
                using var c = ContainerSetup.CreatePicoContainer(
                    ServiceComplexity.SingleDependency,
                    Lifetime.Scoped
                );
            },
            config
        );

        var msResult = Benchmark.Run(
            "MsDI/ContainerSetup",
            () =>
            {
                using var p = ContainerSetup.CreateMsContainer(
                    ServiceComplexity.SingleDependency,
                    Lifetime.Scoped
                );
            },
            config
        );

        return new ComparisonResult
        {
            Name = "ContainerSetup",
            Category = "Infrastructure",
            Baseline = msResult,
            Candidate = picoResult
        };
    }

    private static ComparisonResult RunScopeCreationBenchmark()
    {
        using var picoContainer = ContainerSetup.CreatePicoContainer(
            ServiceComplexity.SingleDependency,
            Lifetime.Scoped
        );
        using var msProvider = ContainerSetup.CreateMsContainer(
            ServiceComplexity.SingleDependency,
            Lifetime.Scoped
        );

        var picoResult = Benchmark.Run(
            "Pico/ScopeCreation",
            () =>
            {
                using var scope = picoContainer.CreateScope();
            },
            Config
        );

        var msResult = Benchmark.Run(
            "MsDI/ScopeCreation",
            () =>
            {
                using var scope = msProvider.CreateScope();
            },
            Config
        );

        return new ComparisonResult
        {
            Name = "ScopeCreation",
            Category = "Infrastructure",
            Baseline = msResult,
            Candidate = picoResult
        };
    }

    private static ComparisonResult RunSingleResolutionBenchmark(Lifetime lifetime)
    {
        var name = $"SingleResolution × {lifetime}";

        using var picoContainer = ContainerSetup.CreatePicoContainer(
            ServiceComplexity.SingleDependency,
            lifetime
        );
        using var msProvider = ContainerSetup.CreateMsContainer(
            ServiceComplexity.SingleDependency,
            lifetime
        );

        var picoResult = Benchmark.RunScoped(
            $"Pico/{name}",
            () => picoContainer.CreateScope(),
            scope => _ = scope.GetService<IServiceWithDep>(),
            Config
        );

        var msResult = Benchmark.RunScoped(
            $"MsDI/{name}",
            () => msProvider.CreateScope(),
            scope =>
                _ = ((IServiceScope)scope).ServiceProvider.GetRequiredService<IServiceWithDep>(),
            Config
        );

        return new ComparisonResult
        {
            Name = name,
            Category = "Resolution",
            Tags = new Dictionary<string, string>
            {
                ["Scenario"] = "SingleResolution",
                ["Lifetime"] = lifetime.ToString()
            },
            Baseline = msResult,
            Candidate = picoResult
        };
    }

    private static ComparisonResult RunMultipleResolutionsBenchmark(Lifetime lifetime)
    {
        var name = $"MultipleResolutions × {lifetime}";
        const int innerLoop = 100;

        using var picoContainer = ContainerSetup.CreatePicoContainer(
            ServiceComplexity.SingleDependency,
            lifetime
        );
        using var msProvider = ContainerSetup.CreateMsContainer(
            ServiceComplexity.SingleDependency,
            lifetime
        );

        var picoResult = Benchmark.RunScoped(
            $"Pico/{name}",
            () => picoContainer.CreateScope(),
            scope =>
            {
                for (int i = 0; i < innerLoop; i++)
                    _ = scope.GetService<IServiceWithDep>();
            },
            Config
        );

        var msResult = Benchmark.RunScoped(
            $"MsDI/{name}",
            () => msProvider.CreateScope(),
            scope =>
            {
                var sp = ((IServiceScope)scope).ServiceProvider;
                for (int i = 0; i < innerLoop; i++)
                    _ = sp.GetRequiredService<IServiceWithDep>();
            },
            Config
        );

        return new ComparisonResult
        {
            Name = name,
            Category = "Resolution",
            Tags = new Dictionary<string, string>
            {
                ["Scenario"] = "MultipleResolutions",
                ["Lifetime"] = lifetime.ToString()
            },
            Baseline = msResult,
            Candidate = picoResult
        };
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

    #endregion
}

#endregion
