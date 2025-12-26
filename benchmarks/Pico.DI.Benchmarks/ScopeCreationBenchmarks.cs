using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Microsoft.Extensions.DependencyInjection;
using Pico.DI.Abs;

namespace Pico.DI.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class ScopeCreationBenchmarks
{
    private SvcContainer _picoContainer = null!;
    private ServiceProvider _msdiProvider = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup Pico.DI
        _picoContainer = new SvcContainer();
        _picoContainer
            .RegisterTransient<ITransientService>(s => new TransientService())
            .RegisterScoped<IScopedService>(s => new ScopedService())
            .RegisterSingleton<ISingletonService>(s => new SingletonService());

        // Setup MS.DI
        var services = new ServiceCollection();
        services.AddTransient<ITransientService, TransientService>();
        services.AddScoped<IScopedService, ScopedService>();
        services.AddSingleton<ISingletonService, SingletonService>();
        _msdiProvider = services.BuildServiceProvider();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _picoContainer.Dispose();
        _msdiProvider.Dispose();
    }

    [Benchmark(Description = "Pico.DI - Create Scope")]
    public ISvcScope Pico_CreateScope() => _picoContainer.CreateScope();

    [Benchmark(Description = "MS.DI - Create Scope")]
    public IServiceScope MSDI_CreateScope() => _msdiProvider.CreateScope();
}
