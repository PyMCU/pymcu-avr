// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/comma-tuple-assign: <c>fill = a, b, c</c> is the tuple
/// <c>(a, b, c)</c>. Adafruit framebuf writes the RGB888 fill that way.
///
/// WHAT DISCRIMINATES: prints 17, 34, 51 (0x11, 0x22, 0x33).
/// </summary>
[TestFixture]
public class CommaTupleAssignTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("comma-tuple-assign"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("comma-tuple-assign"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void AnUnparenthesizedColorTuple_StoresEachByte()
    {
        FullRun(_session).Serial.Text.Should().Contain("17\n34\n51\nEND\n",
            because: "fill = (color>>16)&255, (color>>8)&255, color&255 of 0x112233 is 0x11, 0x22, 0x33");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("17\n34\n51\nEND\n",
            because: "both front ends must wrap a comma RHS as a tuple");
    }
}
