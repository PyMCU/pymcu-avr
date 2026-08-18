// SPDX-License-Identifier: MIT
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integer overflow wraps two's-complement at the store into a typed variable:
/// an int8 at 127 steps to -128, a uint8 at 255 steps to 0, and both directions
/// mirror at the other end of the range. CPython would keep counting past the
/// width; PyMCU counters are machine words, and this fixture pins that
/// divergence for all four widths through both `n = n + 1` and `n += 1`. The
/// counters are seeded from GPIOR0 so every crossing happens at runtime.
/// </summary>
[TestFixture]
public class SignedWraparoundTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("signed-wraparound"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("END\n"), maxMs: 4000);
        return uno;
    }

    [Test]
    public void AllWidths_WrapTwosComplement() =>
        Boot().Serial.Text.Should().Be(
            "WRAP\n" +
            "A\n126\n127\n-128\nY\n-127\n" +
            "B\n126\n127\n-128\n-127\n" +
            "C\n-127\n-128\n127\n" +
            "D\n32766\n32767\n-32768\n-32767\n" +
            "E\n-32767\n-32768\n32767\n" +
            "F\n254\n255\n0\n1\n" +
            "G\n0\n255\n254\n" +
            "H\n65534\n65535\n0\n1\n" +
            "I\n0\n65535\n65534\n" +
            "J\n121\n122\n123\n124\n125\n126\n127\n-128\n-127\n-126\n" +
            "END\n");
}
