namespace Pico.DI.Benchmarks;

/// <summary>
/// Summary of a benchmark timing run.
/// </summary>
public record Summary
{
    public required string Name { get; init; }
    public required double ElapsedMilliseconds { get; init; }
    public required long ElapsedTicks { get; init; }
    public required double ElapsedNanoseconds { get; init; }
    public required ulong CpuCycle { get; init; }
    public required List<GenCount> GenCounts { get; init; }
}

/// <summary>
/// GC generation count delta.
/// </summary>
public record GenCount
{
    public required int Gen { get; init; }
    public required int Count { get; init; }
}

public static class Runner
{
    public static void Initialize()
    {
        if (OperatingSystem.IsWindows())
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
        }

        // Warm-up: touch Stopwatch/GC/cycle APIs once.
        Time("_warmup", 1, static () => { });
    }

    public static Summary Time(string name, int iteration, Action action) =>
        Time(name, iteration, action, setup: null, teardown: null);

    public static Summary Time(
        string name,
        int iteration,
        Action action,
        Action? setup,
        Action? teardown
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iteration);
        ArgumentNullException.ThrowIfNull(action);

        // 1.
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        var gcCounts = Enumerable
            .Range(0, GC.MaxGeneration + 1)
            .Select(GC.CollectionCount)
            .ToArray();

        // 2.
        var watch = Stopwatch.StartNew();
        var cycleCount = GetCycleCount();

        setup?.Invoke();
        for (var i = 0; i < iteration; i++)
            action();

        teardown?.Invoke();
        watch.Stop();
        var cpuCycles = GetCycleCount() - cycleCount;

        var elapsedTicks = watch.ElapsedTicks;
        var elapsedNs = elapsedTicks * (1_000_000_000d / Stopwatch.Frequency);

        // 3.
        return new Summary
        {
            Name = name,
            ElapsedMilliseconds = watch.Elapsed.TotalMilliseconds,
            ElapsedTicks = elapsedTicks,
            ElapsedNanoseconds = elapsedNs,
            CpuCycle = cpuCycles,
            GenCounts = Enumerable
                .Range(0, GC.MaxGeneration + 1)
                .Select(p => new GenCount { Gen = p, Count = GC.CollectionCount(p) - gcCounts[p] })
                .ToList()
        };
    }

    private static ulong GetCycleCount()
    {
        if (!OperatingSystem.IsWindows())
            return 0;

        ulong cycleCount = 0;
        QueryThreadCycleTime(GetCurrentThread(), ref cycleCount);
        return cycleCount;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryThreadCycleTime(IntPtr threadHandle, ref ulong cycleTime);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();
}
