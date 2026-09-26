using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A temporary homed in R16:R17 whose live range crosses an `asm()` that writes the pair:
/// an inlined helper whose asm names R16/R17, and a loop body with such an asm under a
/// `range()` bound. The seed arrives
/// over UART so nothing constant-folds; the expected lines are CPython's for s = 5, with the
/// asm's own effect accounted for where it has one.
/// </summary>
[TestFixture]
public class AsmClobbersTempPairTests
{
    private static int NL(string s) { int n = 0; foreach (var c in s) if (c == '\n') n++; return n; }

    private static List<string> Run(string src, int lines)
    {
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

    private const string Head =
        "from pymcu.hal.uart import UART\n" +
        "from pymcu.chips.atmega328p import GPIOR0, GPIOR1\n" +
        "from pymcu.types import uint8, inline, asm\n\n";

    private const string Seed =
        "uart = UART(9600)\n" +
        "uart.println(\"GO\")\n" +
        "s = uart.read_blocking()\n";

    // The helper's asm overwrote the pair holding the left operand: 26198 for 306.
    [Test]
    public void InlinedAsmThatNamesThePair_LeftTempSurvives()
    {
        Run(Head +
            "@inline\n" +
            "def clob() -> uint8:\n" +
            "    asm(\"ldi r16, 0x55\")\n" +
            "    asm(\"ldi r17, 0x66\")\n" +
            "    return GPIOR0.value + 1\n\n" +
            Seed +
            "print((s + 300) + clob())\n" +
            "print((s ^ 20) + clob())\n" +
            "print((s - 20) + clob())\n" +
            "while True:\n    pass\n", 3)
            .Should().Equal("306", "18", "-14");
    }

    // The range bound lived in R16 across the body: the loop ran once instead of s + 3 times.
    [Test]
    public void AsmInLoopBody_RangeBoundSurvives()
    {
        Run(Head + Seed +
            "GPIOR1.value = 0\n" +
            "for i in range(s + 3):\n" +
            "    asm(\"ldi r16, 0\")\n" +
            "    GPIOR1.value = GPIOR1.value + 1\n" +
            "print(GPIOR1.value)\n" +
            "while True:\n    pass\n", 1)
            .Should().Equal("8");
    }
}
