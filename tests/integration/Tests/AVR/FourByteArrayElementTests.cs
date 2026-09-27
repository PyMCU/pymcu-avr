// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// An int32, uint32 or float array element is four bytes, and CompileArrayLoad /
/// CompileArrayStore moved only a uint16's two and scaled a run-time index by at most two.
/// `buf[i] = v` kept the low byte of v and `buf[i]` read the rest from whatever the
/// registers last held. The field form printed 1004 &amp; 0xFF = 236 (the shape that blocked
/// collections.deque in the MicroPython layer); the module-level form printed the right value
/// only while the registers still held it, so every program here computes something else
/// between the store and the load. Values are seeded from GPIOR0 (0 at reset) so nothing
/// folds; the expected lines are CPython's.
/// </summary>
[TestFixture]
public class FourByteArrayElementTests
{
    private const string Head =
        "from pymcu.types import uint8, int32, uint32, inline\n" +
        "from pymcu.chips.atmega328p import GPIOR0\n" +
        "s = GPIOR0.value\n";

    private static string Run(string body)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(PymcuCompiler.BuildSource(Head + body + "print(\"END\")\n"));
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 3000);
        return uno.Serial.Text.Replace("\r", "");
    }

    [Test]
    public void AnInt32FieldWrittenAndReadThroughInlineMethods_KeepsAllFourBytes()
    {
        Run("class R:\n" +
            "    def __init__(self):\n" +
            "        self._buf: int32[3] = [0] * 3\n" +
            "    @inline\n" +
            "    def put(self, i: uint8, x: int32):\n" +
            "        self._buf[i] = x\n" +
            "    @inline\n" +
            "    def get(self, i: uint8) -> int32:\n" +
            "        return self._buf[i]\n" +
            "r = R()\n" +
            "r.put(s, s + 1004)\n" +
            "r.put(s + 2, s - 2000000000)\n" +
            "print(r.get(s), r.get(s + 1), r.get(s + 2))\n")
            .Should().Contain("1004 0 -2000000000\nEND\n");
    }

    [Test]
    public void AModuleInt32Array_ReadAfterTheRegistersMovedOn()
    {
        Run("g: int32[3] = [0] * 3\n" +
            "g[s] = s + 100000\n" +
            "g[1] = s - 70000\n" +
            "g[s + 1] += s - 3\n" +
            "x: uint8 = s + 3\n" +
            "print(x)\n" +
            "print(g[s], g[s + 1], g[1], g[-1])\n")
            .Should().Contain("3\n100000 -70003 -70003 0\nEND\n");
    }

    [Test]
    public void AFunctionLocalUint32Array_AndOnePastTheByteIndexRange()
    {
        Run("def f(k: uint8) -> uint32:\n" +
            "    b: uint32[3] = [0] * 3\n" +
            "    b[k] = k + 3000000000\n" +
            "    b[k + 1] = b[k] + 1\n" +
            "    y: uint8 = k + 5\n" +
            "    return b[k + 1]\n" +
            "big: int32[80] = [0] * 80\n" +
            "big[s + 70] = s - 1234567\n" +
            "big[79] = s + 7654321\n" +
            "print(f(s), big[s + 70], big[s + 79])\n")
            .Should().Contain("3000000001 -1234567 7654321\nEND\n");
    }

    [Test]
    public void AFloatFieldArray_StoresAndLoadsTheFloatLayout()
    {
        Run("class A:\n" +
            "    def __init__(self):\n" +
            "        self.v: float[3] = [0.0] * 3\n" +
            "    def total(self) -> float:\n" +
            "        t: float = 0.0\n" +
            "        for x in self.v:\n" +
            "            t += x\n" +
            "        return t\n" +
            "a = A()\n" +
            "a.v[s] = s + 1.5\n" +
            "a.v[s + 2] = s - 0.25\n" +
            "print(a.total(), a.v[s + 2], a.v[1])\n")
            .Should().Contain("1.25 -0.25 0.0\nEND\n");
    }
}
