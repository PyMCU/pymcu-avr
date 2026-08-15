using System.Text;

namespace PyMCU.IntegrationTests.Differential;

/// <summary>
/// Compares the optimized and unoptimized traces of one program and, when they differ,
/// says exactly where and how.
/// </summary>
/// <remarks>
/// <para>
/// Only the <b>order</b> of observable events is compared, never their timing or their
/// count. Optimized code reaches the same events sooner, so within a fixed time budget it
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
    public static string? FirstDifference(BehaviorTrace optimized, BehaviorTrace unoptimized)
    {
        if ((optimized.Crash == null) != (unoptimized.Crash == null))
        {
            var (which, crash) = optimized.Crash != null
                ? ("optimized", optimized.Crash)
                : ("unoptimized", unoptimized.Crash!);
            return $"the {which} build faulted and the other did not: {crash}";
        }

        var checkpoints = CompareCheckpoints(optimized.Checkpoints, unoptimized.Checkpoints);
        if (checkpoints != null) return checkpoints;

        var uart = CompareUart(optimized.Uart, unoptimized.Uart);
        if (uart != null) return uart;

        return ComparePins(optimized.Pins, unoptimized.Pins);
    }

    private static string? CompareCheckpoints(
        IReadOnlyList<Checkpoint> optimized, IReadOnlyList<Checkpoint> unoptimized)
    {
        var common = Math.Min(optimized.Count, unoptimized.Count);
        for (var i = 0; i < common; i++)
        {
            if (optimized[i] == unoptimized[i]) continue;
            return
                $"BREAK checkpoint {i + 1} differs in {optimized[i].DifferenceFrom(unoptimized[i])} " +
                "(optimized value first).\n" +
                $"  optimized     : {optimized[i]}\n" +
                $"  unoptimized   : {unoptimized[i]}";
        }
        return null;
    }

    private static string? CompareUart(byte[] optimized, byte[] unoptimized)
    {
        var common = Math.Min(optimized.Length, unoptimized.Length);
        for (var i = 0; i < common; i++)
        {
            if (optimized[i] == unoptimized[i]) continue;
            return
                $"UART byte {i} differs: optimized sent 0x{optimized[i]:X2} ({Printable(optimized[i])}), " +
                $"unoptimized sent 0x{unoptimized[i]:X2} ({Printable(unoptimized[i])}).\n" +
                $"  common prefix : {Quote(optimized, 0, i)}\n" +
                $"  optimized     : {Quote(optimized, i, 32)}\n" +
                $"  unoptimized   : {Quote(unoptimized, i, 32)}";
        }
        return null;
    }

    private static string? ComparePins(IReadOnlyList<PinEvent> optimized, IReadOnlyList<PinEvent> unoptimized)
    {
        var common = Math.Min(optimized.Count, unoptimized.Count);
        for (var i = 0; i < common; i++)
        {
            if (optimized[i] == unoptimized[i]) continue;
            var previous = i == 0 ? "(start)" : optimized[i - 1].ToString();
            return
                $"GPIO change {i} differs: optimized drove {optimized[i]}, " +
                $"unoptimized drove {unoptimized[i]}.\n" +
                $"  after         : {previous}\n" +
                $"  optimized     : {Window(optimized, i, 8)}\n" +
                $"  unoptimized   : {Window(unoptimized, i, 8)}";
        }
        return null;
    }

    /// <summary>A one-line summary of what each side produced, for the failure message.</summary>
    public static string Summarize(BehaviorTrace optimized, BehaviorTrace unoptimized) =>
        $"optimized: {optimized.Uart.Length} UART bytes, {optimized.Pins.Count} GPIO changes, " +
        $"{optimized.Checkpoints.Count} checkpoints, {optimized.Cycles} cycles; " +
        $"unoptimized: {unoptimized.Uart.Length} UART bytes, {unoptimized.Pins.Count} GPIO changes, " +
        $"{unoptimized.Checkpoints.Count} checkpoints, {unoptimized.Cycles} cycles";

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
