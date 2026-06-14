using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Python fidelity for `for ... in range(...)` loops:
///   * the loop variable read inside the body must hold the iteration value (it was bound to a
///     differently-qualified name than the body resolved, so `for i in range(n): acc += i`
///     summed zeros), and
///   * a negative step must count down (the runtime loop used an ascending-only exit test, so
///     `range(hi, lo, -1)` exited immediately).
/// Covered in main and inside a def (the function-qualified name path), with constant and
/// runtime bounds. Values derive from a runtime seed so nothing constant-folds.
/// </summary>
[TestFixture]
public class LoopSemanticsTests
{
    private static List<int> Run(string body, byte seed, int wantLines)
    {
        string src =
            "from pymcu.types import uint8\n" +
            "from pymcu.hal.uart import UART\n\n\n" +
            body +
            "\ndef main():\n" +
            "    uart = UART(9600)\n" +
            "    uart.println(\"GO\")\n" +
            "    s: uint8 = uart.read_blocking()\n" +
            "    run(s)\n" +
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

    // run(s) lives in a def, so the loop variable resolves through the function-qualified
    // ("run.i") name — the exact path that was mismatched.
    [Test]
    public void LoopVariable_And_NegativeStep_InFunction()
    {
        const string body = """
def run(s: uint8):
    uart = UART(9600)
    acc: uint8 = 0
    for i in range(0, 5):
        acc = acc + i
    print(acc)            # 0+1+2+3+4 = 10
    n: uint8 = s
    acc2: uint8 = 0
    for i in range(0, n):
        acc2 = acc2 + i
    print(acc2)           # s=5 -> 0+1+2+3+4 = 10
    step2: uint8 = 0
    for i in range(0, 10, 2):
        step2 = step2 + i
    print(step2)          # 0+2+4+6+8 = 20
    dn: uint8 = 0
    for j in range(5, 0, -1):
        dn = dn + j
    print(dn)             # 5+4+3+2+1 = 15
""";
        Run(body, 5, 4).Should().Equal(new List<int> { 10, 10, 20, 15 });
    }
}
