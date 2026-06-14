using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Arithmetic right shift of negative int16/int32 by whole-byte amounts (8..31) must sign-extend,
/// not shift in zeros. A whole-byte shift takes a byte-move fast path in codegen that previously
/// cleared the vacated high bytes (logical) regardless of signedness. Each shift is checked
/// against a Python oracle over a runtime-derived negative value.
/// </summary>
[TestFixture]
public class SignedShiftWideTests
{
    private static List<long> Run(string typeName, string body, int wantLines)
    {
        string src =
            $"from pymcu.types import {typeName}, uint8\n" +
            "from pymcu.hal.uart import UART\n\n\n" +
            "def main():\n" +
            "    uart = UART(9600)\n" +
            "    uart.println(\"GO\")\n" +
            "    s: uint8 = uart.read_blocking()\n" +
            $"    base: {typeName} = {typeName}(s)\n" +
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
        var outp = new List<long>();
        for (int i = start + 1; i < lines.Length && outp.Count < wantLines; i++)
        {
            var t = lines[i].Trim();
            if (t.Length > 0 && long.TryParse(t, out long v)) outp.Add(v);
        }
        return outp;
    }

    [Test]
    public void Int16_ArithmeticRshift_ByteAligned()
    {
        // v = -5450; arithmetic >> must floor toward -inf.
        var got = Run("int16",
            "    v: int16 = base - 5450\n" +
            "    print(v >> 8)\n    print(v >> 9)\n    print(v >> 12)\n    print(v >> 15)\n", 4);
        got.Should().Equal(new List<long> { -5450 >> 8, -5450 >> 9, -5450 >> 12, -5450 >> 15 });
    }

    [Test]
    public void Int32_ArithmeticRshift_ByteAligned()
    {
        // v = -1000000000; check whole-byte shifts (8,16,24) and intra-byte (15,31).
        const long v = -1000000000;
        var got = Run("int32",
            "    v: int32 = base - 1000000000\n" +
            "    print(v >> 8)\n    print(v >> 15)\n    print(v >> 16)\n    print(v >> 24)\n    print(v >> 31)\n", 5);
        got.Should().Equal(new List<long> { v >> 8, v >> 15, v >> 16, v >> 24, v >> 31 });
    }
}
