using System.Globalization;

namespace PyMCU.TestKit;

/// <summary>
/// One I2C transaction: slave address, direction, data bytes (SLA excluded).
///
/// Text form (the format every oracle in this repo dumps): a write is
/// <c>"&lt;addr hex&gt; &lt;data hex&gt;"</c> -- data may be absent for the
/// zero-length probe, so the line can be just the address and a trailing
/// space -- and a read is <c>"&lt;addr hex&gt; R &lt;data hex&gt;"</c>.
/// </summary>
public sealed record I2cTransaction(byte Address, bool IsWrite, byte[] Data)
{
    public static I2cTransaction ParseLine(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var addr = byte.Parse(parts[0], NumberStyles.HexNumber);
        var isRead = parts.Length > 1 && parts[1] == "R";
        var dataHex = isRead
            ? (parts.Length > 2 ? parts[2] : "")
            : (parts.Length > 1 ? parts[1] : "");
        return new I2cTransaction(addr, !isRead, Convert.FromHexString(dataHex));
    }

    public static List<I2cTransaction> ParseStream(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseLine)
            .ToList();

    public string ToLine() => IsWrite
        ? $"{Address:X2} {Convert.ToHexString(Data).ToLowerInvariant()}"
        : $"{Address:X2} R {Convert.ToHexString(Data).ToLowerInvariant()}";
}
