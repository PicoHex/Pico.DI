using System.Text;

namespace Pico.DI.Benchmarks;

/// <summary>
/// Formats benchmark session results into a complete report.
/// </summary>
public static class BenchmarkReportFormatter
{
    private const int BoxWidth = 78;
    private const int CaseW = 20;
    private const int AvgW = 11;
    private const int P50W = 11;
    private const int P90W = 11;
    private const int P95W = 11;
    private const int P99W = 11;
    private const int CpuW = 11;
    private const int GcW = 14;
    private const int SpeedW = 8;

    private static readonly int[] TableSegments =
    [
        CaseW + 2,
        AvgW + 2,
        P50W + 2,
        P90W + 2,
        P95W + 2,
        P99W + 2,
        CpuW + 2,
        GcW + 2,
        SpeedW + 2
    ];

    /// <summary>
    /// Formats the entire benchmark session into a report string.
    /// </summary>
    public static string Format(BenchmarkSession session)
    {
        var sb = new StringBuilder();

        // Title
        AppendTitleBox(sb, session.Title);
        sb.AppendLine();

        // Comparison report
        AppendTitleBox(sb, "COMPARISON REPORT");
        sb.AppendLine();

        var results = session.Results;
        var byScenario = results.GroupBy(r => r.Scenario).OrderBy(g => g.Key).ToList();

        foreach (var scenarioGroup in byScenario)
        {
            AppendScenarioTable(sb, scenarioGroup.ToList());
            sb.AppendLine();
        }

        // Overall totals
        AppendTitleBox(sb, "TOTALS");
        sb.AppendLine();
        AppendOverallTotals(sb, results.ToList());
        sb.AppendLine();

        // Summary
        AppendSummary(sb, results.ToList());

        return sb.ToString();
    }

    /// <summary>
    /// Formats a single benchmark result as a one-line string.
    /// </summary>
    public static string FormatResultLine(BenchmarkResult result)
    {
        var cyclesPerOp =
            result.CpuCycles == 0
                ? "n/a"
                : (result.CpuCycles / (double)result.IterationsPerSample).ToString("N0");

        return $"avg {result.AvgNs, 8:F1} ns/op | p50 {result.P50Ns, 8:F1} ns/op | cycles/op: {cyclesPerOp, 7} | GCΔ: {ConsoleFormatter.FormatGcDeltas(result.GcGenDeltas)}";
    }

    /// <summary>
    /// Formats a progress line for real-time output.
    /// </summary>
    public static string FormatProgressLine(BenchmarkResult result, int current, int total)
    {
        return $"  [{current}/{total}] {result.Container, -8} × {result.Lifetime, -10}... {FormatResultLine(result)}";
    }

    #region Private Helpers

    private static void AppendTitleBox(StringBuilder sb, string title)
    {
        sb.AppendLine(
            $"{ConsoleFormatter.DoubleLine.TopLeft}{new string(ConsoleFormatter.DoubleLine.Horizontal, BoxWidth)}{ConsoleFormatter.DoubleLine.TopRight}"
        );
        sb.AppendLine(
            $"{ConsoleFormatter.DoubleLine.Vertical}{ConsoleFormatter.Center(title, BoxWidth)}{ConsoleFormatter.DoubleLine.Vertical}"
        );
        sb.AppendLine(
            $"{ConsoleFormatter.DoubleLine.BottomLeft}{new string(ConsoleFormatter.DoubleLine.Horizontal, BoxWidth)}{ConsoleFormatter.DoubleLine.BottomRight}"
        );
    }

    private static void AppendScenarioTable(StringBuilder sb, List<BenchmarkResult> scenarioResults)
    {
        var first = scenarioResults.First();
        sb.AppendLine(
            $"▶ Scenario: {first.Scenario} (samples={first.Samples}, iterationsPerSample={first.IterationsPerSample})"
        );

        sb.AppendLine(ConsoleFormatter.TopLine(TableSegments));
        sb.AppendLine(
            $"│ {"Test case", -CaseW} │ {"Avg(ns/op)", AvgW} │ {"P50(ns/op)", P50W} │ {"P90(ns/op)", P90W} │ {"P95(ns/op)", P95W} │ {"P99(ns/op)", P99W} │ {"CPU(cy)", -CpuW} │ {"GC", -GcW} │ {"Pico x", -SpeedW} │"
        );
        sb.AppendLine(ConsoleFormatter.MiddleLine(TableSegments));

        var lifetimes = new[]
        {
            LifetimeType.Transient,
            LifetimeType.Scoped,
            LifetimeType.Singleton
        };

        foreach (var lifetime in lifetimes)
        {
            var pico = scenarioResults.FirstOrDefault(r =>
                r.Lifetime == lifetime && r.Container == ContainerType.PicoDI
            );
            var ms = scenarioResults.FirstOrDefault(r =>
                r.Lifetime == lifetime && r.Container == ContainerType.MsDI
            );

            if (pico == null || ms == null)
                continue;

            var speedup = pico.AvgNs <= 0 ? "n/a" : (ms.AvgNs / pico.AvgNs).ToString("0.00") + "x";

            var picoCpu = CpuCyclesPerOpString(pico);
            var msCpu = CpuCyclesPerOpString(ms);

            var picoGc = ConsoleFormatter.Truncate(
                ConsoleFormatter.FormatGcDeltas(pico.GcGenDeltas),
                GcW
            );
            var msGc = ConsoleFormatter.Truncate(
                ConsoleFormatter.FormatGcDeltas(ms.GcGenDeltas),
                GcW
            );

            var picoCase = ConsoleFormatter.Truncate($"{ContainerType.PicoDI} × {lifetime}", CaseW);
            var msCase = ConsoleFormatter.Truncate($"{ContainerType.MsDI} × {lifetime}", CaseW);

            sb.AppendLine(
                $"│ {picoCase, -CaseW} │ {pico.AvgNs, AvgW:F1} │ {pico.P50Ns, P50W:F1} │ {pico.P90Ns, P90W:F1} │ {pico.P95Ns, P95W:F1} │ {pico.P99Ns, P99W:F1} │ {ConsoleFormatter.Truncate(picoCpu, CpuW), -CpuW} │ {picoGc, -GcW} │ {ConsoleFormatter.Truncate(speedup, SpeedW), -SpeedW} │"
            );
            sb.AppendLine(
                $"│ {msCase, -CaseW} │ {ms.AvgNs, AvgW:F1} │ {ms.P50Ns, P50W:F1} │ {ms.P90Ns, P90W:F1} │ {ms.P95Ns, P95W:F1} │ {ms.P99Ns, P99W:F1} │ {ConsoleFormatter.Truncate(msCpu, CpuW), -CpuW} │ {msGc, -GcW} │ {"", -SpeedW} │"
            );
        }

        sb.AppendLine(ConsoleFormatter.BottomLine(TableSegments));

        // Scenario totals
        AppendScenarioTotals(sb, scenarioResults);
    }

    private static void AppendScenarioTotals(
        StringBuilder sb,
        List<BenchmarkResult> scenarioResults
    )
    {
        var picoCases = scenarioResults.Where(r => r.Container == ContainerType.PicoDI).ToList();
        var msCases = scenarioResults.Where(r => r.Container == ContainerType.MsDI).ToList();

        if (picoCases.Count == 0 || msCases.Count == 0)
            return;

        var picoAgg = BenchmarkSession.ComputeAggregate(picoCases);
        var msAgg = BenchmarkSession.ComputeAggregate(msCases);

        sb.AppendLine();
        AppendTotalsBlock(sb, "Totals", picoAgg, msAgg);
    }

    private static void AppendOverallTotals(StringBuilder sb, List<BenchmarkResult> results)
    {
        var picoCases = results.Where(r => r.Container == ContainerType.PicoDI).ToList();
        var msCases = results.Where(r => r.Container == ContainerType.MsDI).ToList();

        if (picoCases.Count == 0 || msCases.Count == 0)
            return;

        var picoAgg = BenchmarkSession.ComputeAggregate(picoCases);
        var msAgg = BenchmarkSession.ComputeAggregate(msCases);

        AppendTotalsBlock(sb, "Totals", picoAgg, msAgg);
    }

    private static void AppendTotalsBlock(
        StringBuilder sb,
        string label,
        TotalsAggregate pico,
        TotalsAggregate ms
    )
    {
        var picoGcSum = pico.GcTotals.Values.Sum();
        var msGcSum = ms.GcTotals.Values.Sum();

        const int timeW = 9;
        const int cpuW = 11;
        const int gcW = 14;

        static string FTime(double v) => v.ToString("0.0");

        var picoCpu = ConsoleFormatter.FormatNumber(pico.CpuCyclesPerOp, "N0");
        var msCpu = ConsoleFormatter.FormatNumber(ms.CpuCyclesPerOp, "N0");
        var picoGc = ConsoleFormatter.Truncate(ConsoleFormatter.FormatGcTotals(pico.GcTotals), gcW);
        var msGc = ConsoleFormatter.Truncate(ConsoleFormatter.FormatGcTotals(ms.GcTotals), gcW);

        var cpuRatio =
            pico.CpuCyclesPerOp is null || ms.CpuCyclesPerOp is null
                ? null
                : ConsoleFormatter.Ratio(ms.CpuCyclesPerOp.Value, pico.CpuCyclesPerOp.Value);

        sb.AppendLine($"{label}: (avg across cases)");
        sb.AppendLine(
            $"  {"Pico", -6} | Avg {FTime(pico.AvgNs), timeW} | P50 {FTime(pico.P50Ns), timeW} | P90 {FTime(pico.P90Ns), timeW} | P95 {FTime(pico.P95Ns), timeW} | P99 {FTime(pico.P99Ns), timeW} | CPU {picoCpu, cpuW} | GC {picoGc, -gcW}"
        );
        sb.AppendLine(
            $"  {"Ms", -6} | Avg {FTime(ms.AvgNs), timeW} | P50 {FTime(ms.P50Ns), timeW} | P90 {FTime(ms.P90Ns), timeW} | P95 {FTime(ms.P95Ns), timeW} | P99 {FTime(ms.P99Ns), timeW} | CPU {msCpu, cpuW} | GC {msGc, -gcW}"
        );
        sb.AppendLine(
            $"  {"Pico x", -6} | Avg {ConsoleFormatter.FormatRatio(ConsoleFormatter.Ratio(ms.AvgNs, pico.AvgNs)), timeW} | P50 {ConsoleFormatter.FormatRatio(ConsoleFormatter.Ratio(ms.P50Ns, pico.P50Ns)), timeW} | P90 {ConsoleFormatter.FormatRatio(ConsoleFormatter.Ratio(ms.P90Ns, pico.P90Ns)), timeW} | P95 {ConsoleFormatter.FormatRatio(ConsoleFormatter.Ratio(ms.P95Ns, pico.P95Ns)), timeW} | P99 {ConsoleFormatter.FormatRatio(ConsoleFormatter.Ratio(ms.P99Ns, pico.P99Ns)), timeW} | CPU {ConsoleFormatter.FormatRatio(cpuRatio), cpuW} | GC {ConsoleFormatter.FormatGcRatio(msGcSum, picoGcSum), gcW}"
        );
    }

    private static void AppendSummary(StringBuilder sb, List<BenchmarkResult> results)
    {
        var picoWins = 0;
        var msWins = 0;

        var lifetimes = new[]
        {
            LifetimeType.Transient,
            LifetimeType.Scoped,
            LifetimeType.Singleton
        };
        var scenarios = results.Select(r => r.Scenario).Distinct();

        foreach (var scenario in scenarios)
        {
            foreach (var lifetime in lifetimes)
            {
                var pico = results.FirstOrDefault(r =>
                    r.Scenario == scenario
                    && r.Lifetime == lifetime
                    && r.Container == ContainerType.PicoDI
                );
                var ms = results.FirstOrDefault(r =>
                    r.Scenario == scenario
                    && r.Lifetime == lifetime
                    && r.Container == ContainerType.MsDI
                );

                if (pico == null || ms == null)
                    continue;

                if (pico.AvgNs < ms.AvgNs)
                    picoWins++;
                else
                    msWins++;
            }
        }

        sb.AppendLine(
            $"{ConsoleFormatter.DoubleLine.TopLeft}{new string(ConsoleFormatter.DoubleLine.Horizontal, BoxWidth)}{ConsoleFormatter.DoubleLine.TopRight}"
        );
        sb.AppendLine(
            $"{ConsoleFormatter.DoubleLine.Vertical}{ConsoleFormatter.Center("SUMMARY", BoxWidth)}{ConsoleFormatter.DoubleLine.Vertical}"
        );
        sb.AppendLine($"╠{new string(ConsoleFormatter.DoubleLine.Horizontal, BoxWidth)}╣");

        var total = picoWins + msWins;
        sb.AppendLine(
            $"{ConsoleFormatter.DoubleLine.Vertical}{ConsoleFormatter.Left($"  Pico.DI wins: {picoWins, 3} / {total, 3}", BoxWidth)}{ConsoleFormatter.DoubleLine.Vertical}"
        );
        sb.AppendLine(
            $"{ConsoleFormatter.DoubleLine.Vertical}{ConsoleFormatter.Left($"  Ms.DI wins:   {msWins, 3} / {total, 3}", BoxWidth)}{ConsoleFormatter.DoubleLine.Vertical}"
        );
        sb.AppendLine(
            $"{ConsoleFormatter.DoubleLine.BottomLeft}{new string(ConsoleFormatter.DoubleLine.Horizontal, BoxWidth)}{ConsoleFormatter.DoubleLine.BottomRight}"
        );
    }

    private static string CpuCyclesPerOpString(BenchmarkResult r)
    {
        return r.CpuCycles == 0
            ? "n/a"
            : (r.CpuCycles / (double)r.IterationsPerSample).ToString("N0");
    }

    #endregion
}
