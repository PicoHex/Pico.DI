using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.DependencyInjection;
using Pico.DI.Benchmarks;

// Run BenchmarkDotNet only when explicitly requested (use --bdn).
// Default behavior is the manual Stopwatch-based runner which is AOT-friendly.
var config = DefaultConfig.Instance.WithOptions(ConfigOptions.DisableOptimizationsValidator);

bool runBdn =
    args != null && args.Any(a => string.Equals(a, "--bdn", StringComparison.OrdinalIgnoreCase));

if (runBdn)
{
    BenchmarkRunner.Run<Pico.DI.Benchmarks.ContainerSetupBenchmarks>(config);
    BenchmarkRunner.Run<Pico.DI.Benchmarks.ServiceResolutionBenchmarks>(config);
    Console.WriteLine("BenchmarkDotNet runs finished. Press Enter to exit...");
    Console.ReadLine();
    return;
}

// Manual AOT-friendly benchmarks (Stopwatch loops)
Console.WriteLine("Running native-AOT manual benchmarks (stopwatch loops)...\n");

const int WarmupIterations = 1000;
const int BenchmarkIterations = 1_000_000;

// Warmup
for (int i = 0; i < WarmupIterations; i++)
{
    var c1 = new Pico.DI.SvcContainer();
    c1.RegisterSingleton<ISingletonService>(s => new SingletonService())
        .RegisterScoped<IScopedService>(s => new ScopedService())
        .RegisterTransient<ITransientService>(s => new TransientService())
        .RegisterScoped<IComplexService>(s => new ComplexService(
            s.GetService<ITransientService>(),
            s.GetService<IScopedService>(),
            s.GetService<ISingletonService>()
        ));

    var c2 = new ServiceCollection();
    c2.AddSingleton<ISingletonService, SingletonService>();
    c2.AddScoped<IScopedService, ScopedService>();
    c2.AddTransient<ITransientService, TransientService>();
    c2.AddScoped<IComplexService, ComplexService>();
    c2.BuildServiceProvider();
}

// Pico.DI Container Setup
var sw = Stopwatch.StartNew();
for (int i = 0; i < BenchmarkIterations; i++)
{
    var container = new Pico.DI.SvcContainer();
    container
        .RegisterSingleton<ISingletonService>(s => new SingletonService())
        .RegisterScoped<IScopedService>(s => new ScopedService())
        .RegisterTransient<ITransientService>(s => new TransientService())
        .RegisterScoped<IComplexService>(s => new ComplexService(
            s.GetService<ITransientService>(),
            s.GetService<IScopedService>(),
            s.GetService<ISingletonService>()
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
    services.AddSingleton<ISingletonService, SingletonService>();
    services.AddScoped<IScopedService, ScopedService>();
    services.AddTransient<ITransientService, TransientService>();
    services.AddScoped<IComplexService, ComplexService>();
    services.BuildServiceProvider();
}
sw.Stop();
var msdiSetupNs =
    (double)sw.ElapsedTicks / BenchmarkIterations * 1_000_000_000 / Stopwatch.Frequency;
Console.WriteLine($"  MS.DI - Container Setup:    {msdiSetupNs, 10:F2} ns");
Console.WriteLine($"  ► Pico.DI is {msdiSetupNs / picoSetupNs:F2}x faster\n");

// Setup containers for resolution benchmarks
var picoContainer = new Pico.DI.SvcContainer();
picoContainer
    .RegisterSingleton<ISingletonService>(s => new SingletonService())
    .RegisterScoped<IScopedService>(s => new ScopedService())
    .RegisterTransient<ITransientService>(s => new TransientService())
    .RegisterScoped<IComplexService>(s => new ComplexService(
        s.GetService<ITransientService>(),
        s.GetService<IScopedService>(),
        s.GetService<ISingletonService>()
    ));
using var picoScope = picoContainer.CreateScope();

var msdiServices = new ServiceCollection();
msdiServices.AddSingleton<ISingletonService, SingletonService>();
msdiServices.AddScoped<IScopedService, ScopedService>();
msdiServices.AddTransient<ITransientService, TransientService>();
msdiServices.AddScoped<IComplexService, ComplexService>();
var msdiProvider = msdiServices.BuildServiceProvider();
using var msdiScope = msdiProvider.CreateScope();

// Warmup
for (int i = 0; i < WarmupIterations; i++)
{
    _ = picoScope.GetService<ISingletonService>();
    _ = msdiScope.ServiceProvider.GetService<ISingletonService>();
}

// Pico.DI Singleton Resolution
sw.Restart();
for (int i = 0; i < BenchmarkIterations; i++)
    _ = picoScope.GetService<ISingletonService>();
sw.Stop();
var picoSingletonNs =
    (double)sw.ElapsedTicks / BenchmarkIterations * 1_000_000_000 / Stopwatch.Frequency;
Console.WriteLine($"  Pico.DI - Singleton:        {picoSingletonNs, 10:F2} ns");

// MS.DI Singleton Resolution
sw.Restart();
for (int i = 0; i < BenchmarkIterations; i++)
    _ = msdiScope.ServiceProvider.GetService<ISingletonService>();
sw.Stop();
var msdiSingletonNs =
    (double)sw.ElapsedTicks / BenchmarkIterations * 1_000_000_000 / Stopwatch.Frequency;
Console.WriteLine($"  MS.DI - Singleton:          {msdiSingletonNs, 10:F2} ns");
Console.WriteLine(
    $"  ► {(picoSingletonNs < msdiSingletonNs ? "Pico.DI" : "MS.DI")} is {(picoSingletonNs < msdiSingletonNs ? msdiSingletonNs / picoSingletonNs : picoSingletonNs / msdiSingletonNs):F2}x faster\n"
);

// Transient
for (int i = 0; i < WarmupIterations; i++)
{
    _ = picoScope.GetService<ITransientService>();
    _ = msdiScope.ServiceProvider.GetService<ITransientService>();
}
sw.Restart();
for (int i = 0; i < BenchmarkIterations; i++)
    _ = picoScope.GetService<ITransientService>();
sw.Stop();
var picoTransientNs =
    (double)sw.ElapsedTicks / BenchmarkIterations * 1_000_000_000 / Stopwatch.Frequency;
Console.WriteLine($"  Pico.DI - Transient:        {picoTransientNs, 10:F2} ns");
sw.Restart();
for (int i = 0; i < BenchmarkIterations; i++)
    _ = msdiScope.ServiceProvider.GetService<ITransientService>();
sw.Stop();
var msdiTransientNs =
    (double)sw.ElapsedTicks / BenchmarkIterations * 1_000_000_000 / Stopwatch.Frequency;
Console.WriteLine($"  MS.DI - Transient:          {msdiTransientNs, 10:F2} ns");
Console.WriteLine(
    $"  ► {(picoTransientNs < msdiTransientNs ? "Pico.DI" : "MS.DI")} is {(picoTransientNs < msdiTransientNs ? msdiTransientNs / picoTransientNs : picoTransientNs / msdiTransientNs):F2}x faster\n"
);

// Scoped
for (int i = 0; i < WarmupIterations; i++)
{
    _ = picoScope.GetService<IScopedService>();
    _ = msdiScope.ServiceProvider.GetService<IScopedService>();
}
sw.Restart();
for (int i = 0; i < BenchmarkIterations; i++)
    _ = picoScope.GetService<IScopedService>();
sw.Stop();
var picoScopedNs =
    (double)sw.ElapsedTicks / BenchmarkIterations * 1_000_000_000 / Stopwatch.Frequency;
Console.WriteLine($"  Pico.DI - Scoped:           {picoScopedNs, 10:F2} ns");
sw.Restart();
for (int i = 0; i < BenchmarkIterations; i++)
    _ = msdiScope.ServiceProvider.GetService<IScopedService>();
sw.Stop();
var msdiScopedNs =
    (double)sw.ElapsedTicks / BenchmarkIterations * 1_000_000_000 / Stopwatch.Frequency;
Console.WriteLine($"  MS.DI - Scoped:             {msdiScopedNs, 10:F2} ns");
Console.WriteLine(
    $"  ► {(picoScopedNs < msdiScopedNs ? "Pico.DI" : "MS.DI")} is {(picoScopedNs < msdiScopedNs ? msdiScopedNs / picoScopedNs : picoScopedNs / msdiScopedNs):F2}x faster\n"
);

// Complex
for (int i = 0; i < WarmupIterations; i++)
{
    _ = picoScope.GetService<IComplexService>();
    _ = msdiScope.ServiceProvider.GetService<IComplexService>();
}
sw.Restart();
for (int i = 0; i < BenchmarkIterations; i++)
    _ = picoScope.GetService<IComplexService>();
sw.Stop();
var picoComplexNs =
    (double)sw.ElapsedTicks / BenchmarkIterations * 1_000_000_000 / Stopwatch.Frequency;
Console.WriteLine($"  Pico.DI - Complex:          {picoComplexNs, 10:F2} ns");
sw.Restart();
for (int i = 0; i < BenchmarkIterations; i++)
    _ = msdiScope.ServiceProvider.GetService<IComplexService>();
sw.Stop();
var msdiComplexNs =
    (double)sw.ElapsedTicks / BenchmarkIterations * 1_000_000_000 / Stopwatch.Frequency;
Console.WriteLine($"  MS.DI - Complex:            {msdiComplexNs, 10:F2} ns");
Console.WriteLine(
    $"  ► {(picoComplexNs < msdiComplexNs ? "Pico.DI" : "MS.DI")} is {(picoComplexNs < msdiComplexNs ? msdiComplexNs / picoComplexNs : picoComplexNs / msdiComplexNs):F2}x faster\n"
);

Console.WriteLine("Native AOT manual benchmarks complete. Press Enter to exit...");
Console.ReadLine();
