using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Pico.DI;
using Pico.DI.Abs;

Console.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║           Pico.DI vs MS.DI - Native AOT Benchmark                    ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════════════╣");
Console.WriteLine(
    $"║  Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, -58} ║"
);
Console.WriteLine(
    $"║  AOT:     {(System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported ? "No (JIT)" : "Yes (Native AOT)"), -58} ║"
);
Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

const int WarmupIterations = 1000;
const int BenchmarkIterations = 1_000_000;

// ═══════════════════════════════════════════════════════════════════════════
// Container Setup Benchmark
// ═══════════════════════════════════════════════════════════════════════════
Console.WriteLine("┌──────────────────────────────────────────────────────────────────────┐");
Console.WriteLine("│                     CONTAINER SETUP BENCHMARK                        │");
Console.WriteLine("└──────────────────────────────────────────────────────────────────────┘");

// Warmup
for (int i = 0; i < WarmupIterations; i++)
{
    var c1 = new SvcContainer();
    c1.RegisterSingleton<ILogger>(s => new Logger())
        .RegisterScoped<IRepository>(s => new Repository())
        .RegisterTransient<IService>(s => new SimpleService())
        .RegisterTransient<IComplexService>(s => new ComplexService(
            s.GetService<IService>(),
            s.GetService<IRepository>(),
            s.GetService<ILogger>()
        ));

    var c2 = new ServiceCollection();
    c2.AddSingleton<ILogger, Logger>();
    c2.AddScoped<IRepository, Repository>();
    c2.AddTransient<IService, SimpleService>();
    c2.AddTransient<IComplexService, ComplexService>();
    c2.BuildServiceProvider();
}

// Pico.DI Container Setup
var sw = Stopwatch.StartNew();
for (int i = 0; i < BenchmarkIterations; i++)
{
    var container = new SvcContainer();
    container
        .RegisterSingleton<ILogger>(s => new Logger())
        .RegisterScoped<IRepository>(s => new Repository())
        .RegisterTransient<IService>(s => new SimpleService())
        .RegisterTransient<IComplexService>(s => new ComplexService(
            s.GetService<IService>(),
            s.GetService<IRepository>(),
            s.GetService<ILogger>()
        ));
}
sw.Stop();
var picoSetupNs =
    (double)sw.ElapsedTicks / BenchmarkIterations * 1_000_000_000 / Stopwatch.Frequency;
Console.WriteLine($"  Pico.DI - Container Setup:  {picoSetupNs, 10:F2} ns");

// MS.DI Container Setup
sw.Restart();
for (int i = 0; i < BenchmarkIterations; i++)
{
    var services = new ServiceCollection();
    services.AddSingleton<ILogger, Logger>();
    services.AddScoped<IRepository, Repository>();
    services.AddTransient<IService, SimpleService>();
    services.AddTransient<IComplexService, ComplexService>();
    services.BuildServiceProvider();
}
sw.Stop();
var msdiSetupNs =
    (double)sw.ElapsedTicks / BenchmarkIterations * 1_000_000_000 / Stopwatch.Frequency;
Console.WriteLine($"  MS.DI - Container Setup:    {msdiSetupNs, 10:F2} ns");
Console.WriteLine($"  ► Pico.DI is {msdiSetupNs / picoSetupNs:F2}x faster");
Console.WriteLine();

// ═══════════════════════════════════════════════════════════════════════════
// Service Resolution Benchmark - Singleton
// ═══════════════════════════════════════════════════════════════════════════
Console.WriteLine("┌──────────────────────────────────────────────────────────────────────┐");
Console.WriteLine("│                    SERVICE RESOLUTION - SINGLETON                    │");
Console.WriteLine("└──────────────────────────────────────────────────────────────────────┘");

// Setup containers
var picoContainer = new SvcContainer();
picoContainer
    .RegisterSingleton<ILogger>(s => new Logger())
    .RegisterScoped<IRepository>(s => new Repository())
    .RegisterTransient<IService>(s => new SimpleService())
    .RegisterTransient<IComplexService>(s => new ComplexService(
        s.GetService<IService>(),
        s.GetService<IRepository>(),
        s.GetService<ILogger>()
    ));
using var picoScope = picoContainer.CreateScope();

var msdiServices = new ServiceCollection();
msdiServices.AddSingleton<ILogger, Logger>();
msdiServices.AddScoped<IRepository, Repository>();
msdiServices.AddTransient<IService, SimpleService>();
msdiServices.AddTransient<IComplexService, ComplexService>();
var msdiProvider = msdiServices.BuildServiceProvider();
using var msdiScope = msdiProvider.CreateScope();

// Warmup
for (int i = 0; i < WarmupIterations; i++)
{
    _ = picoScope.GetService<ILogger>();
    _ = msdiScope.ServiceProvider.GetService<ILogger>();
}

// Pico.DI Singleton Resolution
sw.Restart();
for (int i = 0; i < BenchmarkIterations; i++)
{
    _ = picoScope.GetService<ILogger>();
}
sw.Stop();
var picoSingletonNs =
    (double)sw.ElapsedTicks / BenchmarkIterations * 1_000_000_000 / Stopwatch.Frequency;
Console.WriteLine($"  Pico.DI - Singleton:        {picoSingletonNs, 10:F2} ns");

// MS.DI Singleton Resolution
sw.Restart();
for (int i = 0; i < BenchmarkIterations; i++)
{
    _ = msdiScope.ServiceProvider.GetService<ILogger>();
}
sw.Stop();
var msdiSingletonNs =
    (double)sw.ElapsedTicks / BenchmarkIterations * 1_000_000_000 / Stopwatch.Frequency;
Console.WriteLine($"  MS.DI - Singleton:          {msdiSingletonNs, 10:F2} ns");
var singletonRatio = picoSingletonNs / msdiSingletonNs;
Console.WriteLine(
    $"  ► {(singletonRatio < 1 ? "Pico.DI" : "MS.DI")} is {(singletonRatio < 1 ? 1 / singletonRatio : singletonRatio):F2}x faster"
);
Console.WriteLine();

// ═══════════════════════════════════════════════════════════════════════════
// Service Resolution Benchmark - Transient
// ═══════════════════════════════════════════════════════════════════════════
Console.WriteLine("┌──────────────────────────────────────────────────────────────────────┐");
Console.WriteLine("│                    SERVICE RESOLUTION - TRANSIENT                    │");
Console.WriteLine("└──────────────────────────────────────────────────────────────────────┘");

// Warmup
for (int i = 0; i < WarmupIterations; i++)
{
    _ = picoScope.GetService<IService>();
    _ = msdiScope.ServiceProvider.GetService<IService>();
}

// Pico.DI Transient Resolution
sw.Restart();
for (int i = 0; i < BenchmarkIterations; i++)
{
    _ = picoScope.GetService<IService>();
}
sw.Stop();
var picoTransientNs =
    (double)sw.ElapsedTicks / BenchmarkIterations * 1_000_000_000 / Stopwatch.Frequency;
Console.WriteLine($"  Pico.DI - Transient:        {picoTransientNs, 10:F2} ns");

// MS.DI Transient Resolution
sw.Restart();
for (int i = 0; i < BenchmarkIterations; i++)
{
    _ = msdiScope.ServiceProvider.GetService<IService>();
}
sw.Stop();
var msdiTransientNs =
    (double)sw.ElapsedTicks / BenchmarkIterations * 1_000_000_000 / Stopwatch.Frequency;
Console.WriteLine($"  MS.DI - Transient:          {msdiTransientNs, 10:F2} ns");
var transientRatio = picoTransientNs / msdiTransientNs;
Console.WriteLine(
    $"  ► {(transientRatio < 1 ? "Pico.DI" : "MS.DI")} is {(transientRatio < 1 ? 1 / transientRatio : transientRatio):F2}x faster"
);
Console.WriteLine();

// ═══════════════════════════════════════════════════════════════════════════
// Service Resolution Benchmark - Scoped
// ═══════════════════════════════════════════════════════════════════════════
Console.WriteLine("┌──────────────────────────────────────────────────────────────────────┐");
Console.WriteLine("│                      SERVICE RESOLUTION - SCOPED                     │");
Console.WriteLine("└──────────────────────────────────────────────────────────────────────┘");

// Warmup
for (int i = 0; i < WarmupIterations; i++)
{
    _ = picoScope.GetService<IRepository>();
    _ = msdiScope.ServiceProvider.GetService<IRepository>();
}

// Pico.DI Scoped Resolution
sw.Restart();
for (int i = 0; i < BenchmarkIterations; i++)
{
    _ = picoScope.GetService<IRepository>();
}
sw.Stop();
var picoScopedNs =
    (double)sw.ElapsedTicks / BenchmarkIterations * 1_000_000_000 / Stopwatch.Frequency;
Console.WriteLine($"  Pico.DI - Scoped:           {picoScopedNs, 10:F2} ns");

// MS.DI Scoped Resolution
sw.Restart();
for (int i = 0; i < BenchmarkIterations; i++)
{
    _ = msdiScope.ServiceProvider.GetService<IRepository>();
}
sw.Stop();
var msdiScopedNs =
    (double)sw.ElapsedTicks / BenchmarkIterations * 1_000_000_000 / Stopwatch.Frequency;
Console.WriteLine($"  MS.DI - Scoped:             {msdiScopedNs, 10:F2} ns");
var scopedRatio = picoScopedNs / msdiScopedNs;
Console.WriteLine(
    $"  ► {(scopedRatio < 1 ? "Pico.DI" : "MS.DI")} is {(scopedRatio < 1 ? 1 / scopedRatio : scopedRatio):F2}x faster"
);
Console.WriteLine();

// ═══════════════════════════════════════════════════════════════════════════
// Service Resolution Benchmark - Complex (3 dependencies)
// ═══════════════════════════════════════════════════════════════════════════
Console.WriteLine("┌──────────────────────────────────────────────────────────────────────┐");
Console.WriteLine("│                 SERVICE RESOLUTION - COMPLEX (3 deps)                │");
Console.WriteLine("└──────────────────────────────────────────────────────────────────────┘");

// Warmup
for (int i = 0; i < WarmupIterations; i++)
{
    _ = picoScope.GetService<IComplexService>();
    _ = msdiScope.ServiceProvider.GetService<IComplexService>();
}

// Pico.DI Complex Resolution
sw.Restart();
for (int i = 0; i < BenchmarkIterations; i++)
{
    _ = picoScope.GetService<IComplexService>();
}
sw.Stop();
var picoComplexNs =
    (double)sw.ElapsedTicks / BenchmarkIterations * 1_000_000_000 / Stopwatch.Frequency;
Console.WriteLine($"  Pico.DI - Complex:          {picoComplexNs, 10:F2} ns");

// MS.DI Complex Resolution
sw.Restart();
for (int i = 0; i < BenchmarkIterations; i++)
{
    _ = msdiScope.ServiceProvider.GetService<IComplexService>();
}
sw.Stop();
var msdiComplexNs =
    (double)sw.ElapsedTicks / BenchmarkIterations * 1_000_000_000 / Stopwatch.Frequency;
Console.WriteLine($"  MS.DI - Complex:            {msdiComplexNs, 10:F2} ns");
var complexRatio = picoComplexNs / msdiComplexNs;
Console.WriteLine(
    $"  ► {(complexRatio < 1 ? "Pico.DI" : "MS.DI")} is {(complexRatio < 1 ? 1 / complexRatio : complexRatio):F2}x faster"
);
Console.WriteLine();

// ═══════════════════════════════════════════════════════════════════════════
// Summary
// ═══════════════════════════════════════════════════════════════════════════
Console.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                              SUMMARY                                 ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════════════╣");
Console.WriteLine(
    $"║  Container Setup:    Pico.DI {msdiSetupNs / picoSetupNs, 5:F2}x faster                         ║"
);
Console.WriteLine(
    $"║  Singleton Resolve:  {(singletonRatio < 1 ? "Pico.DI" : "MS.DI  ")} {Math.Max(singletonRatio, 1 / singletonRatio), 5:F2}x faster                         ║"
);
Console.WriteLine(
    $"║  Transient Resolve:  {(transientRatio < 1 ? "Pico.DI" : "MS.DI  ")} {Math.Max(transientRatio, 1 / transientRatio), 5:F2}x faster                         ║"
);
Console.WriteLine(
    $"║  Scoped Resolve:     {(scopedRatio < 1 ? "Pico.DI" : "MS.DI  ")} {Math.Max(scopedRatio, 1 / scopedRatio), 5:F2}x faster                         ║"
);
Console.WriteLine(
    $"║  Complex Resolve:    {(complexRatio < 1 ? "Pico.DI" : "MS.DI  ")} {Math.Max(complexRatio, 1 / complexRatio), 5:F2}x faster                         ║"
);
Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");

// Type definitions must come after top-level statements
public interface IService { }

public class SimpleService : IService { }

public interface IRepository { }

public class Repository : IRepository { }

public interface ILogger { }

public class Logger : ILogger { }

public interface IComplexService { }
#pragma warning disable CS9113
public class ComplexService(IService service, IRepository repository, ILogger logger)
    : IComplexService { }
#pragma warning restore CS9113
