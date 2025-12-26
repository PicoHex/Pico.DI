using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Microsoft.Extensions.DependencyInjection;
using Pico.DI.Abs;

namespace Pico.DI.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class ContainerSetupBenchmarks
{
    [Benchmark(Description = "Pico.DI - Container Setup")]
    public ISvcContainer Pico_Setup()
    {
        var container = new SvcContainer();
        container
            .RegisterTransient<ITransientService>(s => new TransientService())
            .RegisterScoped<IScopedService>(s => new ScopedService())
            .RegisterSingleton<ISingletonService>(s => new SingletonService())
            .RegisterScoped<IComplexService>(s => new ComplexService(
                s.GetService<ITransientService>(),
                s.GetService<IScopedService>(),
                s.GetService<ISingletonService>()
            ));
        return container;
    }

    [Benchmark(Description = "MS.DI - Container Setup")]
    public ServiceProvider MSDI_Setup()
    {
        var services = new ServiceCollection();
        services.AddTransient<ITransientService, TransientService>();
        services.AddScoped<IScopedService, ScopedService>();
        services.AddSingleton<ISingletonService, SingletonService>();
        services.AddScoped<IComplexService, ComplexService>();
        return services.BuildServiceProvider();
    }
}
