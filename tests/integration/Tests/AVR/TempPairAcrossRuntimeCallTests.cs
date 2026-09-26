using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A temporary homed in R16:R17 whose live range crosses a math-runtime CALL (`__mul32`,
/// `__div32`, ...) that the IR does not see as a call. Each case prints a value that only
/// comes out right if the temp survives the routine. The seed arrives over UART so nothing
/// constant-folds; the expected lines are CPython's for s = 5.
/// </summary>
[TestFixture]
public class TempPairAcrossRuntimeCallTests
{
    private static int NL(string s) { int n = 0; foreach (var c in s) if (c == '\n') n++; return n; }

    private static List<string> Run(string body, int lines)
    {
        string src =
            "from pymcu.hal.uart import UART\n\n" +
            "uart = UART(9600)\n" +
            "uart.println(\"GO\")\n" +
            "s = uart.read_blocking()\n" +
            body +
            "while True:\n    pass\n";

        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(5);
        uno.RunUntilSerial(uno.Serial, t => NL(t) >= lines + 1, maxMs: 6000);
        var all = uno.Serial.Text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(all, l => l.Trim() == "GO");
        return all.Skip(start + 1).Take(lines).Select(l => l.Trim()).ToList();
    }

    // The peephole's park/unpark collapse took the first CALL __mul32 for the park's
    // redefinition, dropped `MOV R16,R24`, and the second multiply read a stale R16.
    [Test]
    public void PowBaseTemp_ReadAgainAfterMul32()
    {
        Run("print((s + 3) ** 3)\n" +
            "print((s + 20) ** 4)\n" +
            "print((s ^ 20) ** 3)\n", 3)
            .Should().Equal("512", "390625", "4913");
    }
}
