using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Simultaneous (Python-semantics) tuple assignment with overlap between the targets and the
/// RHS: the whole RHS is evaluated before any target is written, so `a, b = b, a` swaps and
/// `c, d, e = e, c, d` rotates. The snapshots are emitted as named variables so the linear
/// copy-propagation cannot forward an alias past the target's reassignment (which had turned the
/// swap into `a = b; b = b`). Values derive from a runtime seed so nothing constant-folds.
/// </summary>
[TestFixture]
public class TupleSwapTests
{
    private static List<int> Run(string body, byte seed, int wantLines)
    {
        string src =
            "from pymcu.types import uint8\n" +
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
        uno.Serial.InjectByte(seed);
        uno.RunUntilSerial(uno.Serial, t => t.Replace("\r", "").Split('\n').Length >= wantLines + 2, maxMs: 4000);
        var lines = uno.Serial.Text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        var outp = new List<int>();
        for (int i = start + 1; i < lines.Length && outp.Count < wantLines; i++)
        {
            var t = lines[i].Trim();
            if (t.Length > 0 && int.TryParse(t, out int v)) outp.Add(v);
        }
        return outp;
    }

    [Test]
    public void Swap_TwoElements()
    {
        // a=5, b=15 -> a, b = b, a -> a=15, b=5
        Run("    a: uint8 = s\n    b: uint8 = s + 10\n    a, b = b, a\n    print(a)\n    print(b)\n", 5, 2)
            .Should().Equal(new List<int> { 15, 5 });
    }

    [Test]
    public void Rotate_ThreeElements()
    {
        // c=6, d=7, e=8 -> c, d, e = e, c, d -> c=8, d=6, e=7
        Run("    c: uint8 = s + 1\n    d: uint8 = s + 2\n    e: uint8 = s + 3\n" +
            "    c, d, e = e, c, d\n    print(c)\n    print(d)\n    print(e)\n", 5, 3)
            .Should().Equal(new List<int> { 8, 6, 7 });
    }
}
