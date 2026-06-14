using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// print() must honour the static signedness of its argument. A negative int8/int16
/// has to print with a leading '-', not the unsigned reading of its byte pattern
/// (e.g. -1 must be "-1", not "255"). Two root causes were fixed together:
///
///   1. EmitPrintBuiltin had no DataType.INT8 case, so an int8 fell through to the
///      unsigned uart_write_decimal_u8 formatter. It now widens to int16 and uses
///      uart_write_decimal_i16.
///   2. Copy propagation forwarded through the width-/signedness-changing copy that a
///      numeric cast emits (int8(s) => Copy(uint8 -> int8)), so the printed value
///      reverted to its unsigned source type. The optimizer now treats such a copy as
///      a barrier (ChangesRepr), keeping the cast's type.
///
/// The seed byte is read at runtime (read_blocking) so nothing constant-folds — the
/// signed widen/format path is actually exercised.
/// </summary>
[TestFixture]
public class SignedPrintTests
{
    private static List<string> RunWithSeed(string src, byte seed, int wantLines)
    {
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(seed);
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

    // int8(0xFF) = -1; (-1) >> 1 = -1. Both must print with the sign.
    [Test]
    public void PrintInt8_Negative_PrintsSigned()
    {
        const string src = """
from pymcu.types import int8, uint8
from pymcu.hal.uart import UART


def main():
    uart = UART(9600)
    uart.println("GO")
    s: uint8 = uart.read_blocking()
    x: int8 = int8(s)
    a: int8 = x >> 1
    print(x)
    print(a)
    while True:
        pass
""";
        RunWithSeed(src, 0xFF, 2).Should().Equal(new List<string> { "-1", "-1" });
    }

    // A positive int8 keeps printing normally (no regression on the common path).
    [Test]
    public void PrintInt8_Positive_PrintsPlain()
    {
        const string src = """
from pymcu.types import int8, uint8
from pymcu.hal.uart import UART


def main():
    uart = UART(9600)
    uart.println("GO")
    s: uint8 = uart.read_blocking()
    x: int8 = int8(s)
    print(x)
    while True:
        pass
""";
        RunWithSeed(src, 0x2A, 1).Should().Equal(new List<string> { "42" });
    }

    // int16 negative path (already had a formatter; guards against the optimizer change
    // perturbing 16-bit signed printing).
    [Test]
    public void PrintInt16_Negative_PrintsSigned()
    {
        const string src = """
from pymcu.types import int16, uint8
from pymcu.hal.uart import UART


def main():
    uart = UART(9600)
    uart.println("GO")
    s: uint8 = uart.read_blocking()
    x: int16 = int16(s) - 500
    print(x)
    while True:
        pass
""";
        // 10 - 500 = -490
        RunWithSeed(src, 10, 1).Should().Equal(new List<string> { "-490" });
    }
}
