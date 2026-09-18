// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/property-tuple-index: <c>return self.measurements[0]</c> on a
/// tuple-returning @property. Adafruit sht4x writes that from
/// <c>temperature</c> / <c>relative_humidity</c>.
///
/// WHAT DISCRIMINATES: prints 10, 20.
/// </summary>
[TestFixture]
public class PropertyTupleIndexTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("property-tuple-index"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("property-tuple-index"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void ATupleProperty_IndexedFromAnotherProperty_PrintsBothElements()
    {
        FullRun(_session).Serial.Text.Should().Contain("10\n20\nEND\n",
            because: "temperature is measurements[0] and humidity is measurements[1]");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("10\n20\nEND\n",
            because: "both front ends must index a tuple-returning @property the same way");
    }
}
