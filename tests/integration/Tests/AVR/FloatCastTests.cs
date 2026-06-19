using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// float(x) builtin: converts an integer (or is the identity on a float) to floating point,
/// matching Python. Previously undefined ("call to undefined function 'float'"). The int->float
/// conversion is the same the implicit mixed-arithmetic path uses. Seed-derived so nothing folds.
/// </summary>
[TestFixture]
public class FloatCastTests
{
    [Test]
    public void FloatOfInt_ConvertsAndComputes()
    {
        const string src = """
from pymcu.types import uint8
from pymcu.hal.uart import UART


def main():
    uart = UART(9600)
    uart.println("GO")
    s: uint8 = uart.read_blocking()
    print(float(s))
    print(float(s) / 2.0)
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
        lines[start + 1].Trim().Should().Be("5.0");
        lines[start + 2].Trim().Should().Be("2.5");
    }
}
