namespace Pico.DI.Benchmarks;

/// <summary>
/// Console formatting utility class providing table drawing, text formatting, and summary output.
/// </summary>
public static class ConsoleFormatter
{
    #region Box Drawing Characters

    /// <summary>Single-line box drawing character set.</summary>
    public static class SingleLine
    {
        public const char TopLeft = '┌';
        public const char TopRight = '┐';
        public const char BottomLeft = '└';
        public const char BottomRight = '┘';
        public const char Horizontal = '─';
        public const char Vertical = '│';
        public const char TopTee = '┬';
        public const char BottomTee = '┴';
        public const char LeftTee = '├';
        public const char RightTee = '┤';
        public const char Cross = '┼';
    }

    /// <summary>Double-line box drawing character set.</summary>
    public static class DoubleLine
    {
        public const char TopLeft = '╔';
        public const char TopRight = '╗';
        public const char BottomLeft = '╚';
        public const char BottomRight = '╝';
        public const char Horizontal = '═';
        public const char Vertical = '║';
    }

    #endregion

    #region Summary Formatting

    /// <summary>
    /// Formats a Summary object into a display string.
    /// </summary>
    /// <param name="summary">The summary to format.</param>
    /// <param name="showName">Whether to include the name in the output.</param>
    /// <param name="showGc">Whether to include GC information.</param>
    public static string FormatSummary(Summary summary, bool showName = true, bool showGc = true)
    {
        var parts = new List<string>();

        if (showName && !string.IsNullOrEmpty(summary.Name))
            parts.Add($"Name: {summary.Name}");

        parts.Add($"Time: {summary.ElapsedMilliseconds:N3}ms ({summary.ElapsedNanoseconds:N0}ns)");

        if (summary.CpuCycle > 0)
            parts.Add($"CPU Cycles: {summary.CpuCycle:N0}");

        if (showGc && summary.GenCounts.Count > 0)
            parts.Add($"GC: {FormatGcDeltas(summary.GenCounts)}");

        return string.Join(" | ", parts);
    }

    /// <summary>
    /// Formats a Summary object into a compact single-line string for inline display.
    /// </summary>
    public static string FormatSummaryCompact(Summary summary)
    {
        var cyclesStr = summary.CpuCycle > 0 ? $"{summary.CpuCycle:N0}" : "n/a";
        var gcStr = FormatGcDeltas(summary.GenCounts);
        return $"{summary.ElapsedMilliseconds:N3}ms | cycles: {cyclesStr} | GC: {gcStr}";
    }

    /// <summary>
    /// Formats a Summary object into a detailed multi-line string.
    /// </summary>
    public static string FormatSummaryDetailed(Summary summary)
    {
        var lines = new List<string>
        {
            $"Name:           {summary.Name}",
            $"Time Elapsed:   {summary.ElapsedMilliseconds:N3}ms",
            $"Ticks:          {summary.ElapsedTicks:N0}",
            $"Nanoseconds:    {summary.ElapsedNanoseconds:N0}ns"
        };

        if (summary.CpuCycle > 0)
            lines.Add($"CPU Cycles:     {summary.CpuCycle:N0}");

        if (summary.GenCounts.Count > 0)
        {
            lines.Add("GC Collections:");
            foreach (var gc in summary.GenCounts)
                lines.Add($"  Gen {gc.Gen}:       {gc.Count}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Formats a Summary for benchmark result output with average ns/op metrics.
    /// Returns a formatted string suitable for console output.
    /// </summary>
    /// <param name="summary">The summary to format.</param>
    /// <param name="avgNsPerOp">Average nanoseconds per operation.</param>
    /// <param name="p50NsPerOp">P50 nanoseconds per operation.</param>
    /// <param name="iterationsPerSample">Number of iterations per sample for CPU cycles calculation.</param>
    public static string FormatBenchmarkResult(
        Summary summary,
        double avgNsPerOp,
        double p50NsPerOp,
        int iterationsPerSample
    )
    {
        var cyclesPerOp =
            summary.CpuCycle > 0
                ? (summary.CpuCycle / (double)iterationsPerSample).ToString("N0")
                : "n/a";

        return $"avg {avgNsPerOp, 8:F1} ns/op | p50 {p50NsPerOp, 8:F1} ns/op | cycles/op: {cyclesPerOp, 7} | GCΔ: {FormatGcDeltas(summary.GenCounts)}";
    }

    #endregion

    #region Table Line Drawing

    /// <summary>
    /// Generates a table separator line.
    /// </summary>
    /// <param name="left">Left border character.</param>
    /// <param name="mid">Middle connector character.</param>
    /// <param name="right">Right border character.</param>
    /// <param name="segments">Array of column widths.</param>
    /// <param name="horizontal">Horizontal line character, defaults to single line.</param>
    public static string Line(
        char left,
        char mid,
        char right,
        int[] segments,
        char horizontal = SingleLine.Horizontal
    )
    {
        var parts = segments.Select(s => new string(horizontal, s));
        return left + string.Join(mid, parts) + right;
    }

    /// <summary>
    /// Generates a table top border line.
    /// </summary>
    public static string TopLine(int[] segments) =>
        Line(SingleLine.TopLeft, SingleLine.TopTee, SingleLine.TopRight, segments);

    /// <summary>
    /// Generates a table middle separator line.
    /// </summary>
    public static string MiddleLine(int[] segments) =>
        Line(SingleLine.LeftTee, SingleLine.Cross, SingleLine.RightTee, segments);

    /// <summary>
    /// Generates a table bottom border line.
    /// </summary>
    public static string BottomLine(int[] segments) =>
        Line(SingleLine.BottomLeft, SingleLine.BottomTee, SingleLine.BottomRight, segments);

    /// <summary>
    /// Prints a double-line bordered title box.
    /// </summary>
    /// <param name="title">Title text.</param>
    /// <param name="width">Box width (excluding border characters).</param>
    public static void PrintTitleBox(string title, int width = 78)
    {
        Console.WriteLine(
            $"{DoubleLine.TopLeft}{new string(DoubleLine.Horizontal, width)}{DoubleLine.TopRight}"
        );
        Console.WriteLine($"{DoubleLine.Vertical}{Center(title, width)}{DoubleLine.Vertical}");
        Console.WriteLine(
            $"{DoubleLine.BottomLeft}{new string(DoubleLine.Horizontal, width)}{DoubleLine.BottomRight}"
        );
    }

    /// <summary>
    /// Prints a double-line bordered content box with title.
    /// </summary>
    public static void PrintContentBox(string title, IEnumerable<string> lines, int width = 78)
    {
        Console.WriteLine(
            $"{DoubleLine.TopLeft}{new string(DoubleLine.Horizontal, width)}{DoubleLine.TopRight}"
        );
        Console.WriteLine($"{DoubleLine.Vertical}{Center(title, width)}{DoubleLine.Vertical}");
        Console.WriteLine($"╠{new string(DoubleLine.Horizontal, width)}╣");

        foreach (var line in lines)
        {
            Console.WriteLine($"{DoubleLine.Vertical}{line.PadRight(width)}{DoubleLine.Vertical}");
        }

        Console.WriteLine(
            $"{DoubleLine.BottomLeft}{new string(DoubleLine.Horizontal, width)}{DoubleLine.BottomRight}"
        );
    }

    #endregion

    #region Text Formatting

    /// <summary>
    /// Truncates text to a specified width, replacing excess with ellipsis.
    /// </summary>
    public static string Truncate(string value, int width)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        if (value.Length <= width)
            return value;
        if (width <= 3)
            return value[..width];
        return value[..(width - 3)] + "...";
    }

    /// <summary>
    /// Centers text within a specified width.
    /// </summary>
    public static string Center(string text, int width)
    {
        if (string.IsNullOrEmpty(text))
            return new string(' ', width);
        if (text.Length >= width)
            return text[..width];
        var left = (width - text.Length) / 2;
        return new string(' ', left) + text + new string(' ', width - left - text.Length);
    }

    /// <summary>
    /// Left-aligns text within a specified width.
    /// </summary>
    public static string Left(string text, int width) => (text ?? string.Empty).PadRight(width);

    /// <summary>
    /// Right-aligns text within a specified width.
    /// </summary>
    public static string Right(string text, int width) => (text ?? string.Empty).PadLeft(width);

    #endregion

    #region Number Formatting

    /// <summary>
    /// Formats a nullable numeric value.
    /// </summary>
    public static string FormatNumber(double? value, string format = "N0", string nullText = "n/a")
    {
        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            return nullText;
        return value.Value.ToString(format);
    }

    /// <summary>
    /// Formats time in nanoseconds.
    /// </summary>
    public static string FormatTime(double nanoseconds, string format = "F1") =>
        nanoseconds.ToString(format);

    /// <summary>
    /// Formats a ratio value (e.g., 2.50x).
    /// </summary>
    public static string FormatRatio(double? value, string nullText = "n/a")
    {
        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            return nullText;
        return value.Value.ToString("0.00") + "x";
    }

    /// <summary>
    /// Calculates a ratio (numerator/denominator), handling edge cases.
    /// </summary>
    public static double? Ratio(double numerator, double denominator)
    {
        if (double.IsNaN(numerator) || double.IsNaN(denominator))
            return null;
        if (double.IsInfinity(numerator) || double.IsInfinity(denominator))
            return null;
        if (denominator <= 0)
            return null;
        return numerator / denominator;
    }

    /// <summary>
    /// Formats a percentage value.
    /// </summary>
    public static string FormatPercent(double value, int decimals = 1) =>
        value.ToString($"F{decimals}") + "%";

    #endregion

    #region GC Formatting

    /// <summary>
    /// Formats GC generation deltas.
    /// </summary>
    /// <param name="deltas">List of GC generation deltas.</param>
    /// <param name="verbose">Whether to use full prefix (Gen vs G).</param>
    public static string FormatGcDeltas(IReadOnlyList<GenCount> deltas, bool verbose = false)
    {
        if (deltas.Count == 0)
            return "0";

        var prefix = verbose ? "Gen" : "G";
        var parts = deltas
            .Where(d => d.Count != 0)
            .Select(d => $"{prefix}{d.Gen}+{d.Count}")
            .ToArray();

        return parts.Length == 0 ? "0" : string.Join(" ", parts);
    }

    /// <summary>
    /// Formats GC generation deltas showing all generations, including those with zero count.
    /// </summary>
    public static string FormatGcDeltasAllGens(IReadOnlyList<GenCount> deltas)
    {
        return deltas.Count == 0
            ? "0"
            : string.Join(" ", deltas.Select(d => $"G{d.Gen}:{d.Count}"));
    }

    /// <summary>
    /// Formats a GC totals dictionary.
    /// </summary>
    public static string FormatGcTotals(Dictionary<int, int> totals)
    {
        if (totals.Count == 0)
            return "0";

        var parts = totals
            .Where(kvp => kvp.Value != 0)
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => $"Gen{kvp.Key}+{kvp.Value}")
            .ToArray();

        return parts.Length == 0 ? "0" : string.Join(" ", parts);
    }

    /// <summary>
    /// Formats a GC ratio string.
    /// </summary>
    public static string FormatGcRatio(int msSum, int picoSum)
    {
        if (picoSum == 0)
            return msSum == 0 ? "1.00x" : "inf";
        return (msSum / (double)picoSum).ToString("0.00") + "x";
    }

    /// <summary>
    /// Aggregates GC generation statistics from multiple results.
    /// </summary>
    public static Dictionary<int, int> SumGcAllGens(
        IEnumerable<IReadOnlyList<GenCount>> gcDeltasList
    )
    {
        var totals = new Dictionary<int, int>();
        foreach (var deltas in gcDeltasList)
        {
            foreach (var d in deltas)
            {
                totals.TryGetValue(d.Gen, out var existing);
                totals[d.Gen] = existing + d.Count;
            }
        }
        return totals;
    }

    #endregion

    #region Progress Display

    /// <summary>
    /// Prints progress information.
    /// </summary>
    /// <param name="current">Current progress value.</param>
    /// <param name="total">Total value.</param>
    /// <param name="message">Additional message.</param>
    public static void PrintProgress(int current, int total, string message = "")
    {
        var percent = (double)current / total * 100;
        Console.Write(
            $"\r[{current}/{total}] {percent:F1}% {message}".PadRight(Console.WindowWidth - 1)
        );
    }

    /// <summary>
    /// Prints a simple progress bar.
    /// </summary>
    /// <param name="progress">Progress value (0-1).</param>
    /// <param name="width">Progress bar width.</param>
    public static void PrintProgressBar(double progress, int width = 50)
    {
        var filled = (int)(progress * width);
        var empty = width - filled;
        Console.Write(
            $"\r[{new string('█', filled)}{new string('░', empty)}] {progress * 100:F1}%"
        );
    }

    #endregion

    #region Table Builder

    /// <summary>
    /// Simple table builder for console output.
    /// </summary>
    public class TableBuilder
    {
        private readonly List<int> _columnWidths = [];
        private readonly List<string[]> _rows = [];
        private string[]? _headers;

        /// <summary>
        /// Sets the table headers.
        /// </summary>
        public TableBuilder SetHeaders(params string[] headers)
        {
            _headers = headers;
            UpdateColumnWidths(headers);
            return this;
        }

        /// <summary>
        /// Adds a data row to the table.
        /// </summary>
        public TableBuilder AddRow(params string[] cells)
        {
            _rows.Add(cells);
            UpdateColumnWidths(cells);
            return this;
        }

        /// <summary>
        /// Outputs the table to console.
        /// </summary>
        public void Print()
        {
            var segments = _columnWidths.Select(w => w + 2).ToArray();

            Console.WriteLine(TopLine(segments));

            if (_headers != null)
            {
                PrintRow(_headers, segments);
                Console.WriteLine(MiddleLine(segments));
            }

            foreach (var row in _rows)
            {
                PrintRow(row, segments);
            }

            Console.WriteLine(BottomLine(segments));
        }

        private void PrintRow(string[] cells, int[] segments)
        {
            var paddedCells = cells.Select(
                (cell, i) =>
                {
                    var width = _columnWidths[i];
                    return $" {(cell ?? "").PadRight(width)} ";
                }
            );
            Console.WriteLine(
                $"{SingleLine.Vertical}{string.Join(SingleLine.Vertical, paddedCells)}{SingleLine.Vertical}"
            );
        }

        private void UpdateColumnWidths(string[] cells)
        {
            for (var i = 0; i < cells.Length; i++)
            {
                var cellWidth = cells[i]?.Length ?? 0;
                if (i >= _columnWidths.Count)
                    _columnWidths.Add(cellWidth);
                else if (cellWidth > _columnWidths[i])
                    _columnWidths[i] = cellWidth;
            }
        }
    }

    #endregion
}
