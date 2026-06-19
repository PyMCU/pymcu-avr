using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// RFC 0001 write-back: a single-field (Model A) ZCA whose non-@inline method mutates its field
/// (e.g. <c>def inc(self, by): self.count += by</c>) used to silently lose the mutation, because
/// the field is passed to the shared outlined body BY VALUE. The body now RETURNS the updated
/// field and the call site copies it back to the instance. Validated end-to-end on the simulator:
///   - straight-line chained mutators accumulate,
///   - a (compile-time unrolled) loop of mutators accumulates across iterations,
///   - a void reset mutator zeroes the field.
/// The amount is a runtime UART seed so nothing constant-folds; results compare against an oracle.
/// </summary>
[TestFixture]
public class WriteBackZcaTests
{
    private static int NL(string s) { int n = 0; foreach (var c in s) if (c == '\n') n++; return n; }

    [Test]
    public void WriteBack_StraightLine_Loop_And_Reset()
    {
        const string src =
            "from pymcu.types import uint8\n" +
            "from pymcu.hal.uart import UART\n\n\n" +
            "class Counter:\n" +
            "    def __init__(self):\n" +
            "        self.count = 0\n\n" +
            "    def inc(self, by: uint8):\n" +
            "        self.count = self.count + by\n\n" +
            "    def reset(self):\n" +
            "        self.count = 0\n\n" +
            "    def get(self) -> uint8:\n" +
            "        return self.count\n\n\n" +
            "def main():\n" +
            "    uart = UART(9600)\n" +
            "    uart.println(\"GO\")\n" +
            "    s: uint8 = uart.read_blocking()\n" +
            "    c = Counter()\n" +
            "    c.inc(s)\n" +            // count = s
            "    c.inc(s)\n" +            // count = 2s
            "    print(c.get())\n" +      // 2s
            "    d = Counter()\n" +
            "    for _ in range(4):\n" +
            "        d.inc(s)\n" +        // count = 4s
            "    print(d.get())\n" +      // 4s
            "    c.reset()\n" +          // count = 0
            "    c.inc(s)\n" +            // count = s
            "    print(c.get())\n" +      // s
            "    while True:\n        pass\n";

        const int seed = 3;
        var expected = new List<long> { 2 * seed, 4 * seed, seed };

        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(seed);
        uno.RunUntilSerial(uno.Serial, t => NL(t) >= 4, maxMs: 6000);

        var lines = uno.Serial.Text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        var got = new List<long>();
        for (int i = start + 1; i < lines.Length && got.Count < expected.Count; i++)
            if (long.TryParse(lines[i].Trim(), out long v)) got.Add(v);

        got.Should().Equal(expected,
            "write-back mutators must persist across chained calls, loop iterations, and a reset");
    }
}
