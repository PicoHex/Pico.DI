namespace Pico.DI.Benchmarks;

/// <summary>
/// Manages a benchmark session, collecting results and providing formatted output.
/// </summary>
public class BenchmarkSession
{
    private readonly List<BenchmarkResult> _results = [];
    private readonly string _title;
    private readonly int _samples;
    private readonly int _iterationsPerSample;
    private readonly int _warmupIterations;
    private readonly int _multipleResolutionsInnerLoop;
    private int _totalTests;
    private int _currentTest;

    /// <summary>
    /// Gets all collected benchmark results.
    /// </summary>
    public IReadOnlyList<BenchmarkResult> Results => _results;

    /// <summary>
    /// Gets the session title.
    /// </summary>
    public string Title => _title;

    /// <summary>
    /// Event raised when a test completes, for real-time progress display.
    /// </summary>
    public event Action<BenchmarkSession, BenchmarkResult, int, int>? OnTestCompleted;

    public BenchmarkSession(
        string title = "Benchmark Session",
        int samples = 25,
        int iterationsPerSample = 10000,
        int warmupIterations = 1000,
        int multipleResolutionsInnerLoop = 100
    )
    {
        _title = title;
        _samples = samples;
        _iterationsPerSample = iterationsPerSample;
        _warmupIterations = warmupIterations;
        _multipleResolutionsInnerLoop = multipleResolutionsInnerLoop;
    }

    /// <summary>
    /// Initializes the runner and configures the benchmark settings.
    /// </summary>
    public BenchmarkSession Initialize()
    {
        Runner.Initialize();
        BenchmarkRunner.Configure(
            warmupIterations: _warmupIterations,
            samples: _samples,
            multipleResolutionsInnerLoop: _multipleResolutionsInnerLoop
        );
        return this;
    }

    /// <summary>
    /// Runs all benchmarks for the specified scenarios, lifetimes, and containers.
    /// </summary>
    public BenchmarkSession RunAll(
        IEnumerable<ContainerType>? containers = null,
        IEnumerable<TestScenario>? scenarios = null,
        IEnumerable<LifetimeType>? lifetimes = null
    )
    {
        containers ??= [ContainerType.PicoDI, ContainerType.MsDI];
        scenarios ??=
        [
            TestScenario.ScopeCreation,
            TestScenario.SingleResolution,
            TestScenario.MultipleResolutions,
            TestScenario.DeepDependencyChain
        ];
        lifetimes ??= [LifetimeType.Transient, LifetimeType.Scoped, LifetimeType.Singleton];

        var containerList = containers.ToList();
        var scenarioList = scenarios.ToList();
        var lifetimeList = lifetimes.ToList();

        _totalTests = containerList.Count * scenarioList.Count * lifetimeList.Count;
        _currentTest = 0;

        foreach (var scenario in scenarioList)
        {
            foreach (var lifetime in lifetimeList)
            {
                BenchmarkRunner.CleanupContainers();

                foreach (var container in containerList)
                {
                    _currentTest++;
                    var result = BenchmarkRunner.Run(
                        container,
                        scenario,
                        lifetime,
                        iterationsPerSample: _iterationsPerSample
                    );
                    _results.Add(result);

                    OnTestCompleted?.Invoke(this, result, _currentTest, _totalTests);
                }
            }
        }

        BenchmarkRunner.CleanupContainers();
        return this;
    }

    /// <summary>
    /// Adds a single benchmark result to the session.
    /// </summary>
    public BenchmarkSession AddResult(BenchmarkResult result)
    {
        _results.Add(result);
        return this;
    }

    /// <summary>
    /// Clears all collected results.
    /// </summary>
    public BenchmarkSession Clear()
    {
        _results.Clear();
        _currentTest = 0;
        return this;
    }

    /// <summary>
    /// Gets the formatted output for the entire session.
    /// </summary>
    public string GetFormattedReport() => BenchmarkReportFormatter.Format(this);

    /// <summary>
    /// Prints the formatted report to console.
    /// </summary>
    public void PrintReport() => Console.Write(GetFormattedReport());

    /// <summary>
    /// Computes aggregate statistics for a subset of results.
    /// </summary>
    public static TotalsAggregate ComputeAggregate(IEnumerable<BenchmarkResult> cases)
    {
        var list = cases.ToList();
        var cpu = list.All(r => r.CpuCycles == 0)
            ? (double?)null
            : list.Average(r => r.CpuCycles / (double)r.IterationsPerSample);

        return new TotalsAggregate(
            AvgNs: list.Average(r => r.AvgNs),
            P50Ns: list.Average(r => r.P50Ns),
            P90Ns: list.Average(r => r.P90Ns),
            P95Ns: list.Average(r => r.P95Ns),
            P99Ns: list.Average(r => r.P99Ns),
            CpuCyclesPerOp: cpu,
            GcTotals: ConsoleFormatter.SumGcAllGens(list.Select(r => r.GcGenDeltas))
        );
    }
}

/// <summary>
/// Aggregate statistics for benchmark results.
/// </summary>
public sealed record TotalsAggregate(
    double AvgNs,
    double P50Ns,
    double P90Ns,
    double P95Ns,
    double P99Ns,
    double? CpuCyclesPerOp,
    Dictionary<int, int> GcTotals
);
