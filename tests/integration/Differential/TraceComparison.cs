using System.Text;

namespace PyMCU.IntegrationTests.Differential;

/// <summary>
/// Names the two builds being compared, so a report reads in the terms of the axis that
/// produced it: <c>optimized</c> vs <c>unoptimized</c> for the IR optimizer, <c>peephole</c>
/// vs <c>no-peephole</c> for the AVR backend peephole.
/// </summary>
/// <param name="A">Label for the first (reference) build.</param>
/// <param name="B">Label for the second build.</param>
public readonly record struct TraceLabels(string A, string B)
{
    public static readonly TraceLabels Optimizer = new("optimized", "unoptimized");
    public static readonly TraceLabels Peephole  = new("peephole", "no-peephole");

    /// <summary>
    /// Both labels padded to a common width, so the report lines line up — including with
    /// the fixed <c>common prefix</c> / <c>after</c> rows, hence the 13-column floor.
    /// </summary>
    internal (string A, string B) Aligned()
    {
        var width = Math.Max(13, Math.Max(A.Length, B.Length));
        return (A.PadRight(width), B.PadRight(width));
    }
}

/// <summary>
/// Compares the two traces of one program and, when they differ, says exactly where and how.
/// </summary>
/// <remarks>
/// <para>
/// Only the <b>order</b> of observable events is compared, never their timing or their
/// count. Faster code reaches the same events sooner, so within a fixed time budget it
/// gets further; the comparison therefore takes the common prefix of each stream. What it
/// asserts is that up to the point both runs reached, they did the same things in the same
/// order — which is precisely the guarantee an optimizer owes its input.
/// </para>
/// <para>
/// UART bytes and GPIO changes are compared as two independent streams rather than one
/// interleaved one. Their relative interleaving is genuinely timing-dependent (a UART byte
/// takes thousands of cycles to shift out, and how many pin writes happen meanwhile depends
/// on how fast the code is), so merging them would manufacture differences that are not bugs.
/// </para>
/// </remarks>
public static class TraceComparison
{
    /// <summary>
    /// Returns null when the two traces agree, or a report of the first difference.
    /// </summary>
    public static string? FirstDifference(BehaviorTrace a, BehaviorTrace b, TraceLabels? labels = null)
    {
        var names = labels ?? TraceLabels.Optimizer;

        if ((a.Crash == null) != (b.Crash == null))
        {
            var (which, crash) = a.Crash != null
                ? (names.A, a.Crash)
                : (names.B, b.Crash!);
            return $"the {which} build faulted and the other did not: {crash}";
        }

        var checkpoints = CompareCheckpoints(a.Checkpoints, b.Checkpoints, names);
        if (checkpoints != null) return checkpoints;

        var uart = CompareUart(a.Uart, b.Uart, names);
        if (uart != null) return uart;

        return ComparePins(a.Pins, b.Pins, names);
    }

    private static string? CompareCheckpoints(
        IReadOnlyList<Checkpoint> a, IReadOnlyList<Checkpoint> b, TraceLabels names)
    {
        var (nameA, nameB) = names.Aligned();
        var common = Math.Min(a.Count, b.Count);
        for (var i = 0; i < common; i++)
        {
            if (a[i] == b[i]) continue;
            return
                $"BREAK checkpoint {i + 1} differs in {a[i].DifferenceFrom(b[i])} " +
                $"({names.A} value first).\n" +
                $"  {nameA} : {a[i]}\n" +
                $"  {nameB} : {b[i]}";
        }
        return null;
    }

    private static string? CompareUart(byte[] a, byte[] b, TraceLabels names)
    {
        var (nameA, nameB) = names.Aligned();
        var common = Math.Min(a.Length, b.Length);
        for (var i = 0; i < common; i++)
        {
            if (a[i] == b[i]) continue;
            return
                $"UART byte {i} differs: {names.A} sent 0x{a[i]:X2} ({Printable(a[i])}), " +
                $"{names.B} sent 0x{b[i]:X2} ({Printable(b[i])}).\n" +
                $"  common prefix : {Quote(a, 0, i)}\n" +
                $"  {nameA} : {Quote(a, i, 32)}\n" +
                $"  {nameB} : {Quote(b, i, 32)}";
        }
        return null;
    }

    private static string? ComparePins(IReadOnlyList<PinEvent> a, IReadOnlyList<PinEvent> b, TraceLabels names)
    {
        var (nameA, nameB) = names.Aligned();
        var common = Math.Min(a.Count, b.Count);
        for (var i = 0; i < common; i++)
        {
            if (a[i] == b[i]) continue;
            var previous = i == 0 ? "(start)" : a[i - 1].ToString();
            return
                $"GPIO change {i} differs: {names.A} drove {a[i]}, " +
                $"{names.B} drove {b[i]}.\n" +
                $"  after         : {previous}\n" +
                $"  {nameA} : {Window(a, i, 8)}\n" +
                $"  {nameB} : {Window(b, i, 8)}";
        }
        return null;
    }

    /// <summary>A one-line summary of what each side produced, for the failure message.</summary>
    public static string Summarize(BehaviorTrace a, BehaviorTrace b, TraceLabels? labels = null)
    {
        var names = labels ?? TraceLabels.Optimizer;
        return
            $"{names.A}: {a.Uart.Length} UART bytes, {a.Pins.Count} GPIO changes, " +
            $"{a.Checkpoints.Count} checkpoints, {a.Cycles} cycles; " +
            $"{names.B}: {b.Uart.Length} UART bytes, {b.Pins.Count} GPIO changes, " +
            $"{b.Checkpoints.Count} checkpoints, {b.Cycles} cycles";
    }

    private static string Printable(byte b) =>
        b is >= 0x20 and < 0x7F ? $"'{(char)b}'" : "non-printable";

    private static string Quote(byte[] bytes, int start, int count)
    {
        var end = Math.Min(bytes.Length, start + count);
        var sb = new StringBuilder("\"");
        for (var i = Math.Max(0, end - count); i < end; i++)
        {
            var b = bytes[i];
            sb.Append(b switch
            {
                (byte)'\n' => "\\n",
                (byte)'\r' => "\\r",
                (byte)'\t' => "\\t",
                >= 0x20 and < 0x7F => ((char)b).ToString(),
                _ => $"\\x{b:X2}",
            });
        }
        return sb.Append('"').ToString();
    }

    private static string Window(IReadOnlyList<PinEvent> events, int index, int count)
    {
        var end = Math.Min(events.Count, index + count);
        return string.Join(", ", Enumerable.Range(index, end - index).Select(i => events[i].ToString()));
    }
}
