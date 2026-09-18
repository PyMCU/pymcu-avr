// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/property-setter-through-loop: <c>pin.direction = OUTPUT</c>
/// through a for-unrolled instance is the <c>@property</c> setter.
/// adafruit_character_lcd.
///
/// WHAT DISCRIMINATES: <c>1</c>, <c>1</c>, END. A compile that still treated
/// <c>direction</c> as a method would refuse the constructor.
/// </summary>
[TestFixture]
public class PropertySetterThroughLoopTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("property-setter-through-loop"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("property-setter-through-loop"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void TheSetterWritesEachUnrolledInstance()
    {
        FullRun(_session).Serial.Text.Should().Contain("1\n1\nEND\n",
            because: "pin.direction = OUTPUT through a for-unrolled instance is the property setter");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("1\n1\nEND\n",
            because: "both front ends must expand the direction setter through the loop var");
    }
}
