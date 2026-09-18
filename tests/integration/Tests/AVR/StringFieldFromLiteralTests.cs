// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/string-field-from-literal: <c>self._message = ""</c> in
/// <c>__init__</c> is a str field, so the setter's <c>str</c> write is
/// the same kind. adafruit_character_lcd.
///
/// WHAT DISCRIMINATES: <c>1</c>, END. A compile that still typed the
/// empty string as uint8 would refuse the setter.
/// </summary>
[TestFixture]
public class StringFieldFromLiteralTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("string-field-from-literal"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("string-field-from-literal"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void EmptyStringFieldAcceptsTheSetter()
    {
        FullRun(_session).Serial.Text.Should().Contain("1\nEND\n",
            because: "self._message = '' is str, so the message setter is the same kind");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("1\nEND\n",
            because: "both front ends must type a string-literal first store as str");
    }
}
