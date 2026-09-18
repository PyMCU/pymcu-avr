// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/for-over-instance-tuple: <c>for pin in (a, b)</c> unrolls over
/// the named instances, which is how adafruit_character_lcd writes
/// <c>for pin in (reset_dio, enable_dio, ...)</c>.
///
/// WHAT DISCRIMINATES: <c>3</c>, <c>5</c>, END. A compile that still required
/// compile-time number/string elements would refuse the constructor.
/// </summary>
[TestFixture]
public class ForOverInstanceTupleTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("for-over-instance-tuple"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("for-over-instance-tuple"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void EachNamedInstanceIsRead()
    {
        FullRun(_session).Serial.Text.Should().Contain("3\n5\nEND\n",
            because: "for p in (a, b) unrolls so each get() reads that instance's compile-time n");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("3\n5\nEND\n",
            because: "both front ends must unroll a tuple of named instances");
    }
}
