using System.Text;

namespace Pico.Bench.Formatters;

/// <summary>
/// Formats benchmark results as Markdown tables for documentation.
/// </summary>
public sealed class MarkdownFormatter : FormatterBase
{
    public MarkdownFormatter(FormatterOptions? options = null)
        : base(options) { }

    public override string Format(BenchmarkResult result)
    {
        return Format([result]);
    }

    public override string Format(IEnumerable<BenchmarkResult> results)
    {
        var sb = new StringBuilder();
        var list = results.ToList();

        if (list.Count == 0)
            return "*No results.*";

        AppendResultsTable(sb, list);
        return sb.ToString();
    }

    public override string Format(ComparisonResult comparison)
    {
        return Format([comparison]);
    }

    public override string Format(IEnumerable<ComparisonResult> comparisons)
    {
        var sb = new StringBuilder();
        var list = comparisons.ToList();

        if (list.Count == 0)
            return "*No comparisons.*";

        AppendComparisonsTable(sb, list);
        return sb.ToString();
    }

    public override string Format(BenchmarkSuite suite)
    {
        var sb = new StringBuilder();

        // Title
        sb.AppendLine($"# {suite.Name}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(suite.Description))
        {
            sb.AppendLine(suite.Description);
            sb.AppendLine();
        }

        // Environment
        if (Options.IncludeEnvironment)
        {
            sb.AppendLine("## Environment");
            sb.AppendLine();
            sb.AppendLine($"**{suite.Environment}**");
            sb.AppendLine();
        }

        if (Options.IncludeTimestamp)
        {
            sb.AppendLine(
                $"> Benchmark run at {suite.Timestamp:yyyy-MM-dd HH:mm:ss} UTC ({suite.Duration.TotalSeconds:F2}s)"
            );
            sb.AppendLine();
        }

        // Results
        if (suite.Results.Count > 0)
        {
            sb.AppendLine("## Results");
            sb.AppendLine();
            AppendResultsTable(sb, suite.Results.ToList());
            sb.AppendLine();
        }

        // Comparisons
        if (suite.Comparisons?.Count > 0)
        {
            sb.AppendLine("## Comparisons");
            sb.AppendLine();
            AppendComparisonsTable(sb, suite.Comparisons.ToList());

            // Summary
            var wins = suite.Comparisons.Count(c => c.IsFaster);
            var total = suite.Comparisons.Count;
            var avgSpeedup = suite.Comparisons.Average(c => c.Speedup);
            var maxSpeedup = suite.Comparisons.Max(c => c.Speedup);

            sb.AppendLine();
            sb.AppendLine("### Summary");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine($"Candidate wins: {wins} / {total}");
            sb.AppendLine($"Average speedup: {FormatSpeedup(avgSpeedup)}");
            sb.AppendLine($"Maximum speedup: {FormatSpeedup(maxSpeedup)}");
            sb.AppendLine("```");
        }

        return sb.ToString();
    }

    #region Results Table

    private void AppendResultsTable(StringBuilder sb, List<BenchmarkResult> results)
    {
        // Header
        sb.Append("| Name | Avg (ns) | P50 (ns) ");
        if (Options.IncludePercentiles)
            sb.Append("| P90 (ns) | P95 (ns) | P99 (ns) ");
        if (Options.IncludeCpuCycles)
            sb.Append("| CPU Cycle ");
        if (Options.IncludeGcInfo)
            sb.Append("| GC (0/1/2) ");
        sb.AppendLine("|");

        // Separator
        sb.Append("|------|----------|----------");
        if (Options.IncludePercentiles)
            sb.Append("|----------|----------|----------");
        if (Options.IncludeCpuCycles)
            sb.Append("|----------");
        if (Options.IncludeGcInfo)
            sb.Append("|------------");
        sb.AppendLine("|");

        // Rows
        foreach (var result in results)
        {
            var s = result.Statistics;
            sb.Append($"| {Escape(result.Name)} | {FormatTime(s.Avg)} | {FormatTime(s.P50)} ");
            if (Options.IncludePercentiles)
                sb.Append($"| {FormatTime(s.P90)} | {FormatTime(s.P95)} | {FormatTime(s.P99)} ");
            if (Options.IncludeCpuCycles)
                sb.Append($"| {s.CpuCyclesPerOp:F0} ");
            if (Options.IncludeGcInfo)
                sb.Append($"| {FormatGcInfo(s.GcInfo)} ");
            sb.AppendLine("|");
        }
    }

    #endregion

    #region Comparisons Table

    private void AppendComparisonsTable(StringBuilder sb, List<ComparisonResult> comparisons)
    {
        // Header
        sb.Append("| Test Case | Baseline (ns) | Candidate (ns) | Speedup |");
        if (Options.IncludeGcInfo)
            sb.Append(" GC |");
        sb.AppendLine();

        // Separator
        sb.Append("|-----------|---------------|----------------|---------|");
        if (Options.IncludeGcInfo)
            sb.Append("----|");
        sb.AppendLine();

        // Rows
        foreach (var c in comparisons)
        {
            var indicator = GetSpeedupIndicator(c.Speedup);
            var speedupText = $"**{FormatSpeedup(c.Speedup)}** {indicator}";

            sb.Append($"| {Escape(c.Name)} ");
            sb.Append($"| {FormatTime(c.Baseline.Statistics.Avg)} ");
            sb.Append($"| {FormatTime(c.Candidate.Statistics.Avg)} ");
            sb.Append($"| {speedupText} |");

            if (Options.IncludeGcInfo)
            {
                var gcStatus = c.Candidate.Statistics.GcInfo.IsZero
                    ? "✓"
                    : c.Candidate.Statistics.GcInfo.ToString();
                sb.Append($" {gcStatus} |");
            }

            sb.AppendLine();
        }
    }

    #endregion

    #region Helpers

    private static string Escape(string value)
    {
        // Escape pipe characters in markdown tables
        return value.Replace("|", "\\|");
    }

    #endregion

    #region Static Helpers

    /// <summary>
    /// Write Markdown to a file, creating directory if needed.
    /// </summary>
    public static void WriteToFile(
        string filePath,
        BenchmarkResult result,
        FormatterOptions? options = null
    )
    {
        var formatter = new MarkdownFormatter(options);
        WriteToFileInternal(filePath, formatter.Format(result));
    }

    /// <summary>
    /// Write Markdown to a file, creating directory if needed.
    /// </summary>
    public static void WriteToFile(
        string filePath,
        IEnumerable<BenchmarkResult> results,
        FormatterOptions? options = null
    )
    {
        var formatter = new MarkdownFormatter(options);
        WriteToFileInternal(filePath, formatter.Format(results));
    }

    /// <summary>
    /// Write Markdown to a file, creating directory if needed.
    /// </summary>
    public static void WriteToFile(
        string filePath,
        IEnumerable<ComparisonResult> comparisons,
        FormatterOptions? options = null
    )
    {
        var formatter = new MarkdownFormatter(options);
        WriteToFileInternal(filePath, formatter.Format(comparisons));
    }

    /// <summary>
    /// Write Markdown to a file, creating directory if needed.
    /// </summary>
    public static void WriteToFile(
        string filePath,
        BenchmarkSuite suite,
        FormatterOptions? options = null
    )
    {
        var formatter = new MarkdownFormatter(options);
        WriteToFileInternal(filePath, formatter.Format(suite));
    }

    /// <summary>
    /// Generate Markdown string for comparisons grouped by category.
    /// </summary>
    public static string FormatGroupedComparisons(
        IEnumerable<ComparisonResult> comparisons,
        Func<ComparisonResult, string> groupBy,
        FormatterOptions? options = null
    )
    {
        var formatter = new MarkdownFormatter(options);
        var sb = new StringBuilder();

        var groups = comparisons.GroupBy(groupBy).OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            sb.AppendLine($"### {group.Key}");
            sb.AppendLine();
            sb.AppendLine(formatter.Format(group.ToList()));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    #endregion
}
