using System.Text;

namespace Pico.Bench.Formatters;

/// <summary>
/// Formats benchmark results for console output with ASCII tables.
/// </summary>
public sealed class ConsoleFormatter : FormatterBase
{
    public ConsoleFormatter(FormatterOptions? options = null)
        : base(options) { }

    public override string Format(BenchmarkResult result)
    {
        var sb = new StringBuilder();
        AppendSingleResult(sb, result);
        return sb.ToString();
    }

    public override string Format(IEnumerable<BenchmarkResult> results)
    {
        var sb = new StringBuilder();
        var list = results.ToList();

        if (list.Count == 0)
            return "No results.";

        AppendResultsTable(sb, list);
        return sb.ToString();
    }

    public override string Format(ComparisonResult comparison)
    {
        var sb = new StringBuilder();
        AppendComparisonResult(sb, comparison);
        return sb.ToString();
    }

    public override string Format(IEnumerable<ComparisonResult> comparisons)
    {
        var sb = new StringBuilder();
        var list = comparisons.ToList();

        if (list.Count == 0)
            return "No comparisons.";

        AppendComparisonsTable(sb, list);
        return sb.ToString();
    }

    public override string Format(BenchmarkSuite suite)
    {
        var sb = new StringBuilder();

        // Header
        AppendBoxHeader(sb, suite.Name, suite.Description);

        // Environment info
        if (Options.IncludeEnvironment)
        {
            sb.AppendLine();
            sb.AppendLine($"Environment: {suite.Environment}");
        }

        if (Options.IncludeTimestamp)
        {
            sb.AppendLine($"Timestamp: {suite.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"Duration: {suite.Duration.TotalSeconds:F2}s");
        }

        // Results
        if (suite.Results.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("═══ Results ═══");
            AppendResultsTable(sb, suite.Results.ToList());
        }

        // Comparisons
        if (suite.Comparisons?.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("═══ Comparisons ═══");
            AppendComparisonsTable(sb, suite.Comparisons.ToList());
        }

        return sb.ToString();
    }

    #region Single Result

    private void AppendSingleResult(StringBuilder sb, BenchmarkResult result)
    {
        sb.AppendLine($"Benchmark: {result.Name}");
        sb.AppendLine($"  Avg: {FormatTime(result.Statistics.Avg)} ns");
        sb.AppendLine($"  P50: {FormatTime(result.Statistics.P50)} ns");

        if (Options.IncludePercentiles)
        {
            sb.AppendLine($"  P90: {FormatTime(result.Statistics.P90)} ns");
            sb.AppendLine($"  P95: {FormatTime(result.Statistics.P95)} ns");
            sb.AppendLine($"  P99: {FormatTime(result.Statistics.P99)} ns");
        }

        if (Options.IncludeCpuCycles)
        {
            sb.AppendLine($"  CPU Cycles: {result.Statistics.CpuCyclesPerOp:F0}");
        }

        if (Options.IncludeGcInfo)
        {
            sb.AppendLine($"  GC (0/1/2): {FormatGcInfo(result.Statistics.GcInfo)}");
        }
    }

    #endregion

    #region Results Table

    private void AppendResultsTable(StringBuilder sb, List<BenchmarkResult> results)
    {
        // Calculate column widths
        var nameWidth = Math.Max(30, results.Max(r => r.Name.Length) + 2);

        // Header
        sb.Append("┌");
        sb.Append(new string('─', nameWidth));
        sb.Append("┬──────────┬──────────");
        if (Options.IncludePercentiles)
            sb.Append("┬──────────┬──────────┬──────────");
        if (Options.IncludeCpuCycles)
            sb.Append("┬───────────");
        if (Options.IncludeGcInfo)
            sb.Append("┬─────────────");
        sb.AppendLine("┐");

        sb.Append("│");
        sb.Append(" Name".PadRight(nameWidth));
        sb.Append("│ Avg (ns) │ P50 (ns) ");
        if (Options.IncludePercentiles)
            sb.Append("│ P90 (ns) │ P95 (ns) │ P99 (ns) ");
        if (Options.IncludeCpuCycles)
            sb.Append("│ CPU Cycle ");
        if (Options.IncludeGcInfo)
            sb.Append("│ GC (0/1/2)  ");
        sb.AppendLine("│");

        sb.Append("├");
        sb.Append(new string('─', nameWidth));
        sb.Append("┼──────────┼──────────");
        if (Options.IncludePercentiles)
            sb.Append("┼──────────┼──────────┼──────────");
        if (Options.IncludeCpuCycles)
            sb.Append("┼───────────");
        if (Options.IncludeGcInfo)
            sb.Append("┼─────────────");
        sb.AppendLine("┤");

        // Rows
        foreach (var result in results)
        {
            var s = result.Statistics;
            sb.Append("│");
            sb.Append($" {result.Name}".PadRight(nameWidth));
            sb.Append($"│{FormatTime(s.Avg), 9} │{FormatTime(s.P50), 9} ");
            if (Options.IncludePercentiles)
                sb.Append(
                    $"│{FormatTime(s.P90), 9} │{FormatTime(s.P95), 9} │{FormatTime(s.P99), 9} "
                );
            if (Options.IncludeCpuCycles)
                sb.Append($"│{s.CpuCyclesPerOp, 10:F0} ");
            if (Options.IncludeGcInfo)
                sb.Append($"│ {FormatGcInfo(s.GcInfo), -11} ");
            sb.AppendLine("│");
        }

        // Footer
        sb.Append("└");
        sb.Append(new string('─', nameWidth));
        sb.Append("┴──────────┴──────────");
        if (Options.IncludePercentiles)
            sb.Append("┴──────────┴──────────┴──────────");
        if (Options.IncludeCpuCycles)
            sb.Append("┴───────────");
        if (Options.IncludeGcInfo)
            sb.Append("┴─────────────");
        sb.AppendLine("┘");
    }

    #endregion

    #region Comparison Result

    private void AppendComparisonResult(StringBuilder sb, ComparisonResult comparison)
    {
        var indicator = GetSpeedupIndicator(comparison.Speedup);

        sb.AppendLine($"Comparison: {comparison.Name}");
        sb.AppendLine(
            $"  Baseline ({comparison.Baseline.Name}): {FormatTime(comparison.Baseline.Statistics.Avg)} ns"
        );
        sb.AppendLine(
            $"  Candidate ({comparison.Candidate.Name}): {FormatTime(comparison.Candidate.Statistics.Avg)} ns"
        );
        sb.AppendLine($"  Speedup: {FormatSpeedup(comparison.Speedup)} {indicator}");
        sb.AppendLine(
            $"  Winner: {(comparison.IsFaster ? comparison.Candidate.Name : comparison.Baseline.Name)}"
        );
    }

    #endregion

    #region Comparisons Table

    private void AppendComparisonsTable(StringBuilder sb, List<ComparisonResult> comparisons)
    {
        var nameWidth = Math.Max(35, comparisons.Max(c => c.Name.Length) + 2);

        // Header
        sb.Append("┌");
        sb.Append(new string('─', nameWidth));
        sb.AppendLine("┬────────────┬────────────┬──────────┐");

        sb.Append("│");
        sb.Append(" Test Case".PadRight(nameWidth));
        sb.AppendLine("│ Baseline   │ Candidate  │ Speedup  │");

        sb.Append("├");
        sb.Append(new string('─', nameWidth));
        sb.AppendLine("┼────────────┼────────────┼──────────┤");

        // Rows
        foreach (var c in comparisons)
        {
            var indicator = GetSpeedupIndicator(c.Speedup);
            sb.Append("│");
            sb.Append($" {c.Name}".PadRight(nameWidth));
            sb.Append($"│{FormatTime(c.Baseline.Statistics.Avg), 10} ");
            sb.Append($"│{FormatTime(c.Candidate.Statistics.Avg), 10} ");
            sb.AppendLine($"│{FormatSpeedup(c.Speedup), 7} {indicator} │");
        }

        // Footer
        sb.Append("└");
        sb.Append(new string('─', nameWidth));
        sb.AppendLine("┴────────────┴────────────┴──────────┘");

        // Summary
        var wins = comparisons.Count(c => c.IsFaster);
        var avgSpeedup = comparisons.Average(c => c.Speedup);
        var maxSpeedup = comparisons.Max(c => c.Speedup);

        sb.AppendLine();
        sb.AppendLine(
            $"Summary: Candidate wins {wins}/{comparisons.Count} | Avg: {FormatSpeedup(avgSpeedup)} | Max: {FormatSpeedup(maxSpeedup)}"
        );
    }

    #endregion

    #region Box Header

    private static void AppendBoxHeader(StringBuilder sb, string title, string? description)
    {
        const int width = 80;
        var border = new string('═', width - 2);

        sb.AppendLine($"╔{border}╗");
        sb.AppendLine($"║{CenterText(title, width - 2)}║");

        if (!string.IsNullOrEmpty(description))
        {
            sb.AppendLine($"║{CenterText(description, width - 2)}║");
        }

        sb.AppendLine($"╚{border}╝");
    }

    private static string CenterText(string text, int width)
    {
        if (text.Length >= width)
            return text[..width];

        var padding = (width - text.Length) / 2;
        return text.PadLeft(padding + text.Length).PadRight(width);
    }

    #endregion

    #region Static Helpers

    /// <summary>
    /// Write formatted results directly to console.
    /// </summary>
    public static void Write(BenchmarkResult result, FormatterOptions? options = null)
    {
        var formatter = new ConsoleFormatter(options);
        Console.WriteLine(formatter.Format(result));
    }

    /// <summary>
    /// Write formatted results directly to console.
    /// </summary>
    public static void Write(IEnumerable<BenchmarkResult> results, FormatterOptions? options = null)
    {
        var formatter = new ConsoleFormatter(options);
        Console.WriteLine(formatter.Format(results));
    }

    /// <summary>
    /// Write formatted comparison directly to console.
    /// </summary>
    public static void Write(ComparisonResult comparison, FormatterOptions? options = null)
    {
        var formatter = new ConsoleFormatter(options);
        Console.WriteLine(formatter.Format(comparison));
    }

    /// <summary>
    /// Write formatted comparisons directly to console.
    /// </summary>
    public static void Write(
        IEnumerable<ComparisonResult> comparisons,
        FormatterOptions? options = null
    )
    {
        var formatter = new ConsoleFormatter(options);
        Console.WriteLine(formatter.Format(comparisons));
    }

    /// <summary>
    /// Write formatted suite directly to console.
    /// </summary>
    public static void Write(BenchmarkSuite suite, FormatterOptions? options = null)
    {
        var formatter = new ConsoleFormatter(options);
        Console.WriteLine(formatter.Format(suite));
    }

    #endregion
}
