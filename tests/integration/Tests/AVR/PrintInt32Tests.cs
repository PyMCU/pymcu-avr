using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// print(int32) must format with its sign over the full 32-bit range. Previously INT32 fell
/// through to uart_write_decimal_i16, truncating to 16 bits; it now routes to the new
/// uart_write_decimal_i32 (sign + uart_write_decimal_u32 magnitude). Values are derived from a
/// runtime seed so nothing constant-folds.
/// </summary>
[TestFixture]
public class PrintInt32Tests
{
    private static List<string> Run(string body, int wantLines)
    {
        string src =
            "from pymcu.types import int32, uint8\n" +
            "from pymcu.hal.uart import UART\n\n\n" +
            "def main():\n" +
            "    uart = UART(9600)\n" +
            "    uart.println(\"GO\")\n" +
            "    s: uint8 = uart.read_blocking()\n" +
            "    base: int32 = int32(s)\n" +     // = 0 at runtime, compiler-opaque
            body +
            "    while True:\n        pass\n";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(0);
        uno.RunUntilSerial(uno.Serial, s => s.Replace("\r", "").Split('\n').Length >= wantLines + 2, maxMs: 4000);

        var lines = uno.Serial.Text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        var outp = new List<string>();
        for (int i = start + 1; i < lines.Length && outp.Count < wantLines; i++)
        {
            var t = lines[i].Trim();
            if (t.Length > 0) outp.Add(t);
        }
        return outp;
    }

    [Test]
    public void FullRange_PrintsSignedDecimal()
    {
        // base == 0 at runtime, so each prints its literal — but not constant-folded.
        var got = Run(
            "    a: int32 = base + 2147483647\n    print(a)\n" +       // INT32_MAX
            "    b: int32 = base + (-2147483647 - 1)\n    print(b)\n" + // INT32_MIN
            "    c: int32 = base + -1\n    print(c)\n" +
            "    d: int32 = base + -1000000\n    print(d)\n" +
            "    e: int32 = base + 1000000\n    print(e)\n" +
            "    f: int32 = base + -42\n    print(f)\n", 6);
        got.Should().Equal(new List<string>
        {
            "2147483647", "-2147483648", "-1", "-1000000", "1000000", "-42",
        });
    }
}
