// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/local-class-shadows-imported: <c>DigitalInOut(pin, self)</c>
/// inside a module that also <c>import digitalio</c> is that module's
/// two-argument class, even when the entry file did
/// <c>from digitalio import DigitalInOut</c>. adafruit_74hc595.
///
/// WHAT DISCRIMINATES: <c>7</c>, END. Binding digitalio's 1-argument
/// constructor would refuse the call as too many arguments.
/// </summary>
[TestFixture]
public class LocalClassShadowsImportedTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("local-class-shadows-imported"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("local-class-shadows-imported"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void GetPinConstructsTheLocalClass()
    {
        FullRun(_session).Serial.Text.Should().Contain("7\nEND\n",
            because: "get_pin's DigitalInOut(pin, self) is the module's class, not digitalio's");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("7\nEND\n",
            because: "both front ends must keep the local DigitalInOut on get_pin");
    }
}
