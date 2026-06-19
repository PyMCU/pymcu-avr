using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Constant-folded % must use Python's floor-mod (the remainder follows the sign of the
/// divisor), matching the // folding which already floors. C#'s % truncates toward zero
/// (remainder follows the dividend), so -7 % 2 folded to -1 instead of Python's 1.
///
/// These are all compile-time constants, so they exercise the optimizer's BinaryOp.Mod
/// folding directly (no runtime division routine is involved). Runtime signed //,% is a
/// separate, still-open gap (see codegen backlog A38).
/// </summary>
[TestFixture]
public class ConstFloorModTests
{
    private static List<string> Run(string body, int wantLines)
    {
        string src =
            "from pymcu.types import int16\n" +
            "from pymcu.hal.uart import UART\n\n\n" +
            "def main():\n" +
            "    uart = UART(9600)\n" +
            "    uart.println(\"GO\")\n" +
            body +
            "    while True:\n        pass\n";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, s => s.Replace("\r", "").Split('\n').Length >= wantLines + 2, maxMs: 3000);

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
    public void NegDividend_FollowsDivisorSign() =>
        Run("    a: int16 = -7 % 2\n    print(a)\n", 1)
            .Should().Equal(new List<string> { "1" });   // Python -7 % 2 = 1 (not C# -1)

    [Test]
    public void NegDivisor_FollowsDivisorSign() =>
        Run("    a: int16 = 7 % -2\n    print(a)\n", 1)
            .Should().Equal(new List<string> { "-1" });  // Python 7 % -2 = -1

    [Test]
    public void BothNegative() =>
        Run("    a: int16 = -7 % -2\n    print(a)\n", 1)
            .Should().Equal(new List<string> { "-1" });  // Python -7 % -2 = -1

    [Test]
    public void NonDivisible_NegDividend() =>
        Run("    a: int16 = -7 % 3\n    print(a)\n", 1)
            .Should().Equal(new List<string> { "2" });   // Python -7 % 3 = 2 (not C# -1)

    [Test]
    public void NonNegativeUnaffected() =>
        Run("    a: int16 = 7 % 3\n    print(a)\n", 1)
            .Should().Equal(new List<string> { "1" });   // unchanged
}
