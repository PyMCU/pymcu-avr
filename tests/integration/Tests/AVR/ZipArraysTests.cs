using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// zip(a, b) over two fixed arrays whose elements are runtime values — previously rejected
/// ("zip() array elements must be compile-time integer constants"). Now iterates element-wise,
/// binding each loop variable to the element (Copy / ArrayLoad). Seed makes a[0] runtime.
/// </summary>
[TestFixture]
public class ZipArraysTests
{
    [Test]
    public void ZipTwoRuntimeArrays_SumsProducts()
    {
        const string src = """
from pymcu.types import uint8
from pymcu.hal.uart import UART


def main():
    uart = UART(9600)
    uart.println("GO")
    s: uint8 = uart.read_blocking()
    a: uint8[3] = [1, 2, 3]
    b: uint8[3] = [10, 20, 30]
    a[0] = a[0] + (s - 5)
    acc: uint8 = 0
    for x, y in zip(a, b):
        acc = acc + x * y
    print(acc)
    while True:
        pass
""";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(5);   // a[0] stays 1
        uno.RunUntilSerial(uno.Serial, t => t.Replace("\r", "").Split('\n').Length >= 3, maxMs: 3000);
        var lines = uno.Serial.Text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        lines[start + 1].Trim().Should().Be("140");   // 1*10 + 2*20 + 3*30
    }
}
