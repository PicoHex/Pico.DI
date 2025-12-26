namespace Pico.DI.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class DeepDependencyBenchmarks
{
    private SvcContainer _picoContainer = null!;
    private ISvcScope _picoScope = null!;
    private ServiceProvider _msdiProvider = null!;
    private IServiceScope _msdiScope = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Pico.DI: type mapping注册，与MS.DI一致
        _picoContainer = new SvcContainer();
        _picoContainer
            .RegisterTransient<ILevel1, Level1>()
            .RegisterTransient<ILevel2, Level2>()
            .RegisterTransient<ILevel3, Level3>()
            .RegisterTransient<ILevel4, Level4>()
            .RegisterTransient<ILevel5, Level5>();
        _picoContainer.Build();
        _picoScope = _picoContainer.CreateScope();

        // MS.DI: type mapping注册
        var services = new ServiceCollection();
        services.AddTransient<ILevel1, Level1>();
        services.AddTransient<ILevel2, Level2>();
        services.AddTransient<ILevel3, Level3>();
        services.AddTransient<ILevel4, Level4>();
        services.AddTransient<ILevel5, Level5>();
        _msdiProvider = services.BuildServiceProvider();
        _msdiScope = _msdiProvider.CreateScope();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _picoScope.Dispose();
        _picoContainer.Dispose();
        _msdiScope.Dispose();
        _msdiProvider.Dispose();
    }

    [Benchmark(Description = "Pico.DI - Deep (5 levels)")]
    public ILevel5 Pico_Deep() => _picoScope.GetService<ILevel5>();

    [Benchmark(Description = "MS.DI - Deep (5 levels)")]
    public ILevel5 MSDI_Deep() => _msdiScope.ServiceProvider.GetRequiredService<ILevel5>();
}
