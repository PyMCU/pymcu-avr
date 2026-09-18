// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/super-init-nested-field: <c>self.format = Fmt()</c> inside
/// an imported base <c>__init__</c> <c>if</c>, reached through
/// <c>super()</c> on <c>class OLED(framebuf.FrameBuffer)</c>. Adafruit
/// ssd1306 writes that on <c>FrameBuffer.__init__</c> after
/// <c>import adafruit_framebuf as framebuf</c>.
///
/// WHAT DISCRIMINATES: prints 7.
/// </summary>
[TestFixture]
public class SuperInitNestedFieldTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("super-init-nested-field"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("super-init-nested-field"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void SuperInit_FieldAssignedInABaseIf_PrintsTheNestedField()
    {
        FullRun(_session).Serial.Text.Should().Contain("7\nEND\n",
            because: "self.format = Fmt() inside the base __init__ if is a constructor field");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("7\nEND\n",
            because: "both front ends must treat a super-expanded base ctor as inside __init__");
    }
}
