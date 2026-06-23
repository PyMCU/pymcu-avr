using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Regression tests for AVR backend correctness bugs found by code review.
/// Each test compiles a program that exercises the exact miscompiled path and checks the
/// simulated runtime result, so it fails on the pre-fix codegen and passes after.
/// </summary>
[TestFixture]
public class BugfixRegressionTests
{
    // F5: an 8-bit shift by a count >= 8 masked the count with `& 7`, so `x << 8` / `x >> 8`
    // emitted no shifts and returned the operand unchanged instead of 0.
    [Test]
    public void Shift8Bit_ByEight_YieldsZero()
    {
        const string src = """
from pymcu.types import uint8
from pymcu.hal.uart import UART


def main():
    uart = UART(9600)
    uart.println("GO")
    s: uint8 = uart.read_blocking()
    y: uint8 = s << 8
    print(y)
    z: uint8 = s >> 8
    print(z)
    while True:
        pass
""";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(5);          // non-zero input so the bug (return operand) is visible
        uno.RunUntilSerial(uno.Serial, t => t.Replace("\r", "").Split('\n').Length >= 4, maxMs: 3000);

        var lines = uno.Serial.Text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        lines[start + 1].Trim().Should().Be("0", "uint8 5 << 8 truncates to 0");
        lines[start + 2].Trim().Should().Be("0", "uint8 5 >> 8 is 0");
    }

    // F1: a function with two float arguments built arg0 in R22:R25 then overwrote R22:R25 while
    // building arg1, corrupting arg0. pick_first returns arg0, so the bug surfaces as arg1's value.
    [Test]
    public void TwoFloatArgs_Arg0NotClobberedByArg1()
    {
        const string src = """
from pymcu.types import uint8
from pymcu.hal.uart import UART


def pick_first(a: float, b: float) -> float:
    return a


def main():
    uart = UART(9600)
    uart.println("GO")
    s: uint8 = uart.read_blocking()
    c: float = float(s)
    d: float = c + 100.0
    print(pick_first(c, d))
    while True:
        pass
""";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(5);          // c = 5.0, d = 105.0; pick_first must return 5.0, not 105.0
        uno.RunUntilSerial(uno.Serial, t => t.Replace("\r", "").Split('\n').Length >= 3, maxMs: 3000);

        var lines = uno.Serial.Text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        lines[start + 1].Trim().Should().Be("5.0", "arg0 must survive building arg1");
    }

    // Float `//` must floor the quotient toward -inf, not truncate (it previously mapped to plain
    // division: 5.0 // 2.0 gave 2.5 instead of 2.0, and -5.0 // 2.0 gave -2.5 instead of -3.0).
    [Test]
    public void FloatFloorDiv_FloorsTowardNegInf()
    {
        const string src = """
from pymcu.types import uint8
from pymcu.hal.uart import UART


def main():
    uart = UART(9600)
    uart.println("GO")
    s: uint8 = uart.read_blocking()
    print(float(s) // 2.0)
    print((0.0 - float(s)) // 2.0)
    while True:
        pass
""";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(5);          // 5.0 // 2.0 == 2.0 ; -5.0 // 2.0 == -3.0
        uno.RunUntilSerial(uno.Serial, t => t.Replace("\r", "").Split('\n').Length >= 4, maxMs: 4000);

        var lines = uno.Serial.Text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        lines[start + 1].Trim().Should().Be("2.0", "5.0 // 2.0 floors to 2.0");
        lines[start + 2].Trim().Should().Be("-3.0", "-5.0 // 2.0 floors toward -inf to -3.0");
    }

    // A narrow value passed through a function pointer to a wider parameter must be zero/sign
    // extended; previously only R24 was loaded, leaving the uint16 arg's high byte as garbage.
    [Test]
    public void FunctionPointer_NarrowArgWidenedToWideParam()
    {
        const string src = """
from pymcu.types import uint8, uint16, Callable
from pymcu.hal.uart import UART


def echo16(x: uint16) -> uint16:
    return x


def main():
    uart = UART(9600)
    uart.println("GO")
    s: uint8 = uart.read_blocking()
    fn: Callable = echo16
    r: uint16 = fn(s)
    print(r)
    print(r)
    while True:
        pass
""";
        var hex = PymcuCompiler.BuildSource(src);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(5);          // echo16(5) must return 5, not 5 + garbage*256
        uno.RunUntilSerial(uno.Serial, t => t.Replace("\r", "").Split('\n').Length >= 4, maxMs: 3000);

        var lines = uno.Serial.Text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        lines[start + 1].Trim().Should().Be("5", "uint8 arg must zero-extend to the uint16 param");
    }
}
