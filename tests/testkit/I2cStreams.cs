using System.Text;
using NUnit.Framework;

namespace PyMCU.TestKit;

/// <summary>
/// Byte-for-byte comparison of two I2C transaction streams. The failure text
/// names the first differing transaction index, the byte offset inside it,
/// and a short hex window on either side -- never a whole-buffer dump.
/// </summary>
public static class I2cStreams
{
    public static void AssertEqual(
        IReadOnlyList<I2cTransaction> expected, WireTrace actual, string label)
    {
        var actualTxns = actual.Transactions;
        var crashNote = actual.Crash != null ? $" [{actual.Crash}]" : "";
        var common = Math.Min(expected.Count, actualTxns.Count);
        for (var i = 0; i < common; i++)
        {
            var e = expected[i];
            var a = actualTxns[i];
            if (e.Address != a.Address || e.IsWrite != a.IsWrite)
            {
                Assert.Fail(
                    $"[{label}] first differing transaction index {i}: " +
                    $"expected addr 0x{e.Address:X2} {Dir(e.IsWrite)}, " +
                    $"actual addr 0x{a.Address:X2} {Dir(a.IsWrite)}{crashNote}");
            }

            var limit = Math.Min(e.Data.Length, a.Data.Length);
            for (var b = 0; b < limit; b++)
            {
                if (e.Data[b] == a.Data[b]) continue;
                Assert.Fail(
                    $"[{label}] transaction {i} (addr 0x{e.Address:X2} {Dir(e.IsWrite)}, " +
                    $"{e.Data.Length} bytes) first differs at byte offset {b}: " +
                    $"expected 0x{e.Data[b]:X2}, actual 0x{a.Data[b]:X2}{crashNote}\n" +
                    $"expected window: {Window(e.Data, b)}\n" +
                    $"actual   window: {Window(a.Data, b)}");
            }

            if (e.Data.Length != a.Data.Length)
            {
                var ev = limit < e.Data.Length ? $"0x{e.Data[limit]:X2}" : "(absent)";
                var av = limit < a.Data.Length ? $"0x{a.Data[limit]:X2}" : "(absent)";
                Assert.Fail(
                    $"[{label}] transaction {i} (addr 0x{e.Address:X2} {Dir(e.IsWrite)}) " +
                    $"length differs: expected {e.Data.Length} bytes, actual {a.Data.Length} bytes; " +
                    $"common prefix equal, first differing byte offset {limit}: " +
                    $"expected {ev}, actual {av}{crashNote}\n" +
                    $"expected window: {Window(e.Data, limit)}\n" +
                    $"actual   window: {Window(a.Data, limit)}");
            }
        }

        if (expected.Count != actualTxns.Count)
        {
            var extra = expected.Count < actualTxns.Count;
            var tx = extra ? actualTxns[common] : expected[common];
            Assert.Fail(
                $"[{label}] transaction count differs: expected {expected.Count}, " +
                $"actual {actualTxns.Count}; first {(extra ? "extra" : "missing")} transaction " +
                $"at index {common} (addr 0x{tx.Address:X2} {Dir(tx.IsWrite)}, " +
                $"{tx.Data.Length} bytes){crashNote}");
        }
    }

    private static string Dir(bool isWrite) => isWrite ? "write" : "read";

    /// <summary>Short hex dump around <paramref name="offset"/>: never dumps a whole buffer.</summary>
    private static string Window(byte[] data, int offset, int radius = 8)
    {
        var lo = Math.Max(0, offset - radius);
        var hi = Math.Min(data.Length, offset + radius + 1);
        if (lo >= hi) return "(empty)";
        var sb = new StringBuilder();
        sb.Append($"[{lo}..{hi - 1}]");
        for (var i = lo; i < hi; i++)
            sb.Append(i == offset ? $" >>{data[i]:X2}<<" : $" {data[i]:X2}");
        return sb.ToString();
    }
}
