using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The walrus operator (:=) must persist its assignment to the named variable, not only produce
/// the value for the enclosing expression. The target was stored under the bare name while the
/// body resolved the function-qualified name, so a later read saw 0. Seed-derived; not folded.
/// </summary>
[TestFixture]
public class WalrusTests
{
    private static int Newlines(string s) { int n = 0; foreach (var c in s) if (c == '\n') n++; return n; }

    [Test]
    public void Walrus_PersistsAndComputes()
    {
        const string src = """
from pymcu.types import uint8
from pymcu.hal.uart import UART


def main():
    uart = UART(9600)
    uart.println("GO")
    s: uint8 = uart.read_blocking()
    if (w := s + 7) > 10:
        print(w)
    print(w)
    y: uint8 = (z := s * 2) + 1
    print(z)
    print(y)
    while True:
        pass
""";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(5);
        uno.RunUntilSerial(uno.Serial, t => Newlines(t) >= 5, maxMs: 4000);   // GO + 4 values
        var lines = uno.Serial.Text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        var got = new List<int>();
        for (int i = start + 1; i < lines.Length && got.Count < 4; i++)
            if (int.TryParse(lines[i].Trim(), out int v)) got.Add(v);
        got.Should().Equal(new List<int> { 12, 12, 10, 11 });
    }
}
