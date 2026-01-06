namespace Pico.DI.Benchmarks;

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

    public static Summary Time(string name, int iteration, Action action)
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
        for (var i = 0; i < iteration; i++)
            action();
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
