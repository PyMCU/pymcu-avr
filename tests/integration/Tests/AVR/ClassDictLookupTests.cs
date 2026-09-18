// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/class-dict-lookup: a class-body dict read as <c>self.vals[n]</c>
/// from a method of an imported class. Adafruit <c>VEML7700.gain_value</c>
/// is this shape; the subscript was refused as a bit index.
///
/// WHAT DISCRIMINATES: <c>25</c>.
/// </summary>
[TestFixture]
public class ClassDictLookupTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("class-dict-lookup"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("class-dict-lookup"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void AClassDictFloatValue_PrintsTheScaledLookup()
    {
        FullRun(_session).Serial.Should().ContainLine("25",
            because: "vals[2] is 0.25, times 100 is 25");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("25\nEND\n",
            because: "both front ends must look up a class-level dict through self");
    }
}
