using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A tuple[T, U] return annotation on an @inline tuple-returning function now parses (the type
/// annotation parser rejected the comma: "Expected ']'"). The annotation is documentation — the
/// caller's unpack targets receive the values. minmax(8, 5) -> (5, 8).
/// </summary>
[TestFixture]
public class TupleReturnAnnTests
{
    [Test]
    public void TupleAnnotation_ParsesAndReturns()
    {
        const string src = """
from pymcu.types import uint8, inline
from pymcu.hal.uart import UART


@inline
def minmax(a: uint8, b: uint8) -> tuple[uint8, uint8]:
    if a < b:
        return a, b
    return b, a


def main():
    uart = UART(9600)
    uart.println("GO")
    s: uint8 = uart.read_blocking()
    lo: uint8 = 0
    hi: uint8 = 0
    lo, hi = minmax(s + 3, s)
    print(lo)
    print(hi)
    while True:
        pass
""";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(5);
        uno.RunUntilSerial(uno.Serial, t => t.Replace("\r", "").Split('\n').Length >= 4, maxMs: 3000);
        var lines = uno.Serial.Text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        var got = new List<int>();
        for (int i = start + 1; i < lines.Length && got.Count < 2; i++)
            if (int.TryParse(lines[i].Trim(), out int v)) got.Add(v);
        got.Should().Equal(new List<int> { 5, 8 });
    }
}
