namespace Pico.DI.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class ServiceResolutionBenchmarks
{
    private SvcContainer _picoContainer = null!;
    private ISvcScope _picoScope = null!;

    private ServiceProvider _msdiProvider = null!;
    private IServiceScope _msdiScope = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup Pico.DI
        _picoContainer = new SvcContainer();
        _picoContainer
            .RegisterTransient<ITransientService>(s => new TransientService())
            .RegisterScoped<IScopedService>(s => new ScopedService())
            .RegisterSingleton<ISingletonService>(s => new SingletonService())
            .RegisterScoped<IComplexService>(s => new ComplexService(
                s.GetService<ITransientService>(),
                s.GetService<IScopedService>(),
                s.GetService<ISingletonService>()
            ));

        _picoScope = _picoContainer.CreateScope();

        // Setup MS.DI
        var services = new ServiceCollection();
        services.AddTransient<ITransientService, TransientService>();
        services.AddScoped<IScopedService, ScopedService>();
        services.AddSingleton<ISingletonService, SingletonService>();
        services.AddScoped<IComplexService, ComplexService>();

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

    // === Transient Resolution ===

    [Benchmark(Description = "Pico.DI - Transient")]
    public ITransientService Pico_Transient() => _picoScope.GetService<ITransientService>();

    [Benchmark(Description = "MS.DI - Transient")]
    public ITransientService MSDI_Transient() =>
        _msdiScope.ServiceProvider.GetRequiredService<ITransientService>();

    // === Scoped Resolution ===

    [Benchmark(Description = "Pico.DI - Scoped")]
    public IScopedService Pico_Scoped() => _picoScope.GetService<IScopedService>();

    [Benchmark(Description = "MS.DI - Scoped")]
    public IScopedService MSDI_Scoped() =>
        _msdiScope.ServiceProvider.GetRequiredService<IScopedService>();

    // === Singleton Resolution ===

    [Benchmark(Description = "Pico.DI - Singleton")]
    public ISingletonService Pico_Singleton() => _picoScope.GetService<ISingletonService>();

    [Benchmark(Description = "MS.DI - Singleton")]
    public ISingletonService MSDI_Singleton() =>
        _msdiScope.ServiceProvider.GetRequiredService<ISingletonService>();

    // === Complex Service with Dependencies ===

    [Benchmark(Description = "Pico.DI - Complex (3 deps)")]
    public IComplexService Pico_Complex() => _picoScope.GetService<IComplexService>();

    [Benchmark(Description = "MS.DI - Complex (3 deps)")]
    public IComplexService MSDI_Complex() =>
        _msdiScope.ServiceProvider.GetRequiredService<IComplexService>();
}
