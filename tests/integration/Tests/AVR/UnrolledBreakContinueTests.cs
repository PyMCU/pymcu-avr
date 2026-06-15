using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// break/continue inside a compile-time-unrolled for-each (over a fixed array, and over an array
/// slice) — previously rejected ("Continue statement outside of loop"). When the body uses
/// break/continue, each unrolled iteration is now bracketed with a per-iteration continue label
/// and a shared break label; loops without them keep the plain unroll. Runtime-seeded so the
/// array's first element is not folded away.
/// </summary>
[TestFixture]
public class UnrolledBreakContinueTests
{
    [Test]
    public void ContinueAndBreak_InFixedArrayAndSlice()
    {
        const string src = """
from pymcu.types import uint8
from pymcu.hal.uart import UART


def main():
    uart = UART(9600)
    uart.println("GO")
    s: uint8 = uart.read_blocking()
    arr: uint8[5] = [10, 20, 30, 40, 50]
    arr[0] = arr[0] + (s - 5)
    a: uint8 = 0
    for v in arr:
        if v == 30:
            continue
        a = a + v
    print(a)
    b: uint8 = 0
    for v in arr:
        if v == 40:
            break
        b = b + v
    print(b)
    c: uint8 = 0
    for v in arr[1:4]:
        if v == 30:
            continue
        c = c + v
    print(c)
    while True:
        pass
""";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(5);   // arr[0] stays 10
        uno.RunUntilSerial(uno.Serial, t => t.Replace("\r", "").Split('\n').Length >= 5, maxMs: 4000);
        var lines = uno.Serial.Text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        var got = new List<int>();
        for (int i = start + 1; i < lines.Length && got.Count < 3; i++)
            if (int.TryParse(lines[i].Trim(), out int v)) got.Add(v);
        got.Should().Equal(new List<int> { 120, 60, 60 });   // 10+20+40+50, 10+20+30, 20+40
    }

    [Test]
    public void ContinueAndBreak_InEnumerateAndReversed()
    {
        const string src = """
from pymcu.types import uint8
from pymcu.hal.uart import UART


def main():
    uart = UART(9600)
    uart.println("GO")
    s: uint8 = uart.read_blocking()
    arr: uint8[4] = [10, 20, 30, 40]
    arr[0] = arr[0] + (s - 5)
    a: uint8 = 0
    for i, v in enumerate(arr):
        if v == 30:
            continue
        a = a + v
    print(a)
    b: uint8 = 0
    for v in reversed(arr):
        if v == 20:
            break
        b = b + v
    print(b)
    while True:
        pass
""";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(5);
        uno.RunUntilSerial(uno.Serial, t => t.Replace("\r", "").Split('\n').Length >= 4, maxMs: 4000);
        var lines = uno.Serial.Text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        var got = new List<int>();
        for (int i = start + 1; i < lines.Length && got.Count < 2; i++)
            if (int.TryParse(lines[i].Trim(), out int v)) got.Add(v);
        got.Should().Equal(new List<int> { 70, 70 });   // enumerate skip 30: 10+20+40 ; reversed break at 20: 40+30
    }

    [Test]
    public void ContinueAndBreak_InListLiteral()
    {
        const string src = """
from pymcu.types import uint8
from pymcu.hal.uart import UART


def main():
    uart = UART(9600)
    uart.println("GO")
    a: uint8 = 0
    for x in [10, 20, 30, 40]:
        if x == 30:
            continue
        a = a + x
    print(a)
    b: uint8 = 0
    for x in [10, 20, 30, 40]:
        if x == 30:
            break
        b = b + x
    print(b)
    while True:
        pass
""";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.RunUntilSerial(uno.Serial, t => t.Replace("\r", "").Split('\n').Length >= 4, maxMs: 3000);
        var lines = uno.Serial.Text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        var got = new List<int>();
        for (int i = start + 1; i < lines.Length && got.Count < 2; i++)
            if (int.TryParse(lines[i].Trim(), out int v)) got.Add(v);
        got.Should().Equal(new List<int> { 70, 30 });   // continue skip 30: 10+20+40 ; break at 30: 10+20
    }
}
