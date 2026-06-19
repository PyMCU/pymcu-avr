using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// `for v in arr` over an SRAM-resident array (one made runtime-indexed, so it lives in memory
/// rather than as per-element arr__k vars) must read each element with an indexed load. The
/// unrolled forward for-each previously read the missing element vars and summed 0. Verified for
/// uint8 and uint16 element types; a runtime index/read forces the SRAM layout.
/// </summary>
[TestFixture]
public class SramArrayForEachTests
{
    private static int NL(string s) { int n = 0; foreach (var c in s) if (c == '\n') n++; return n; }

    [Test]
    public void ForEach_OverSramArray_ReadsElements()
    {
        const string src = """
from pymcu.types import uint8, uint16
from pymcu.hal.uart import UART


def main():
    uart = UART(9600)
    uart.println("GO")
    s: uint8 = uart.read_blocking()
    b: uint8[3] = [10, 20, 30]
    j: uint8 = s - 5
    print(b[j])
    t2: uint8 = 0
    for v in b:
        t2 = t2 + v
    print(t2)
    arr: uint16[4] = [1000, 2000, 3000, 4000]
    i: uint8 = s - 3
    print(arr[i])
    tt: uint16 = 0
    for v in arr:
        tt = tt + v
    print(tt)
    while True:
        pass
""";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(5);
        uno.RunUntilSerial(uno.Serial, t => NL(t) >= 5, maxMs: 4000);
        var lines = uno.Serial.Text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        var got = new List<int>();
        for (int i = start + 1; i < lines.Length && got.Count < 4; i++)
            if (int.TryParse(lines[i].Trim(), out int v)) got.Add(v);
        got.Should().Equal(new List<int> { 10, 60, 3000, 10000 });
    }
}
