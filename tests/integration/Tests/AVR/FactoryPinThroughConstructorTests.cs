// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/factory-pin-through-constructor: <c>Lcd(mcp.get_pin(1), ...)</c>
/// where <c>Lcd.__init__</c> annotates the pins as <c>digitalio.DigitalInOut</c>
/// must still call the expander's <c>high()</c>. adafruit_character_lcd.
///
/// WHAT DISCRIMINATES: <c>1</c>, <c>2</c>, END. Binding digitalio's
/// <c>high()</c> would print 99.
/// </summary>
[TestFixture]
public class FactoryPinThroughConstructorTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("factory-pin-through-constructor"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("factory-pin-through-constructor"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void TheFactoryClassWinsOverTheAnnotation()
    {
        var text = FullRun(_session).Serial.Text;
        text.Should().Contain("7\nEND\n",
            because: "mcp.get_pin() through Lcd.__init__ is the expander DigitalInOut, not digitalio's");
        text.Should().NotContain("99",
            because: "the annotation digitalio.DigitalInOut must not win over the factory's class");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("7\nEND\n",
            because: "both front ends must keep the factory DigitalInOut inside the constructor");
    }
}
