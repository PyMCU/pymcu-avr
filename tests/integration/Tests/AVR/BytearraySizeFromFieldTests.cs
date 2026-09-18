// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/bytearray-size-from-field: <c>bytearray(self._n)</c> after
/// <c>self._n = 1</c> is a compile-time size. adafruit_74hc595.
///
/// WHAT DISCRIMINATES: <c>7</c>, END. A compile that still refused
/// <c>bytearray(self._n)</c> would not build.
/// </summary>
[TestFixture]
public class BytearraySizeFromFieldTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("bytearray-size-from-field"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("bytearray-size-from-field"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void TheBufferIsSizedFromTheField()
    {
        FullRun(_session).Serial.Text.Should().Contain("7\nEND\n",
            because: "bytearray(self._n) after self._n = 1 is a compile-time size");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("7\nEND\n",
            because: "both front ends must size a bytearray from a constant field");
    }
}
