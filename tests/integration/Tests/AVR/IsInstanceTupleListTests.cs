// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/isinstance-tuple-list (PyMCU#423): <c>isinstance(address, (tuple, list))</c>
/// folds from the argument's shape at the constructor call site.
/// adafruit_ht16k33's HT16K33.__init__.
///
/// WHAT DISCRIMINATES: 112 (the int default 0x70) and 1 (the list's first
/// element). A compile that still refused isinstance would not build.
/// </summary>
[TestFixture]
[Property("Issue", "423")]
public class IsInstanceTupleListTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("isinstance-tuple-list"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("isinstance-tuple-list"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void AnIntAndAListEachTakeTheirArm()
    {
        FullRun(_session).Serial.Text.Should().Contain("112\n1\nEND\n",
            because: "0x70 is the int arm and 1 is the list arm; a refused isinstance would not build");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("112\n1\nEND\n",
            because: "both front ends must fold isinstance(address, (tuple, list)) the same way");
    }
}
