// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/field-tuple-literal: a tuple of constants assigned to a field and
/// indexed as <c>self.scale[n]</c> from a method of an imported class.
/// Adafruit <c>DPS310.__init__</c> is this shape; the tuple was refused as a
/// runtime value.
///
/// WHAT DISCRIMINATES: <c>70</c>.
/// </summary>
[TestFixture]
public class FieldTupleLiteralTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("field-tuple-literal"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("field-tuple-literal"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void ATupleField_PrintsTheIndexedConstant()
    {
        FullRun(_session).Serial.Should().ContainLine("70",
            because: "scale[6] is 70");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("70\nEND\n",
            because: "both front ends must store a constant tuple in a field and index it");
    }
}
