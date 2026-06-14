using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Iterating a fixed-array slice in a for-loop (for v in arr[lo:hi:step]) — previously rejected
/// ("for-in iterable must be ..."). The slice unrolls over its index range (constant bounds,
/// negative indices and open ends normalized like elsewhere), binding the loop variable to each
/// element. Runtime-seeded array values so nothing folds to a constant sum.
/// </summary>
[TestFixture]
public class SliceIterTests
{
    [Test]
    public void SliceForms_SumCorrectly()
    {
        const string src = """
from pymcu.types import uint8
from pymcu.hal.uart import UART


def main():
    uart = UART(9600)
    uart.println("GO")
    s: uint8 = uart.read_blocking()
    arr: uint8[5] = [1, 2, 3, 4, 5]
    arr[0] = arr[0] + (s - 5)
    t: uint8 = 0
    for v in arr[1:4]:
        t = t + v
    print(t)
    u: uint8 = 0
    for v in arr[2:]:
        u = u + v
    print(u)
    w: uint8 = 0
    for v in arr[:2]:
        w = w + v
    print(w)
    x: uint8 = 0
    for v in arr[::2]:
        x = x + v
    print(x)
    while True:
        pass
""";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(5);   // arr[0] stays 1
        uno.RunUntilSerial(uno.Serial, t => t.Replace("\r", "").Split('\n').Length >= 6, maxMs: 4000);
        var lines = uno.Serial.Text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        var got = new List<int>();
        for (int i = start + 1; i < lines.Length && got.Count < 4; i++)
            if (int.TryParse(lines[i].Trim(), out int v)) got.Add(v);
        got.Should().Equal(new List<int> { 9, 12, 3, 9 });   // 2+3+4, 3+4+5, 1+2, 1+3+5
    }
}
