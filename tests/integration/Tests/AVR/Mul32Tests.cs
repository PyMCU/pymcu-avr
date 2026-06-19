using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Unsigned 32-bit multiplication (low 32 bits) via __mul32. The is32 Mul case previously fell
/// through to an 8-bit MUL, silently truncating (5000000 -> 64). Operands are derived from a
/// runtime seed so nothing constant-folds; results are compared against a C# oracle masked to
/// 32 bits, including a product that overflows 2^32.
/// </summary>
[TestFixture]
public class Mul32Tests
{
    private static int NL(string s) { int n = 0; foreach (var c in s) if (c == '\n') n++; return n; }

    [Test]
    public void Mul32_LowWord_MatchesOracle()
    {
        // base = s = 5. Each operand = base + (target - 5), i.e. the target at runtime.
        (long a, long b)[] pairs =
        {
            (5, 1000000),       // 5000000
            (70000, 70000),     // 4_900_000_000 -> wraps mod 2^32
            (65536, 65536),     // 2^32 -> wraps to 0
            (16777216, 200),    // 2^24 * 200 -> wraps
            (123456, 9999),     // 1_234_437_744
        };
        var body = new System.Text.StringBuilder();
        var expected = new List<long>();
        int n = 0;
        foreach (var (a, b) in pairs)
        {
            body.Append($"    a{n}: uint32 = uint32(s) + {a - 5}\n");
            body.Append($"    b{n}: uint32 = uint32(s) + {b - 5}\n");
            body.Append($"    p{n}: uint32 = a{n} * b{n}\n");
            body.Append($"    print(p{n})\n");
            expected.Add(unchecked((uint)(a * b)));
            n++;
        }
        string src =
            "from pymcu.types import uint8, uint32\n" +
            "from pymcu.hal.uart import UART\n\n\n" +
            "def main():\n" +
            "    uart = UART(9600)\n" +
            "    uart.println(\"GO\")\n" +
            "    s: uint8 = uart.read_blocking()\n" +
            body +
            "    while True:\n        pass\n";

        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(5);
        uno.RunUntilSerial(uno.Serial, t => NL(t) >= pairs.Length + 1, maxMs: 6000);
        var lines = uno.Serial.Text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        var got = new List<long>();
        for (int i = start + 1; i < lines.Length && got.Count < pairs.Length; i++)
            if (long.TryParse(lines[i].Trim(), out long v)) got.Add(v);
        got.Should().Equal(expected);
    }
}
