// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/imported-dotted-super: <c>super().__init__</c> on
/// <c>class OLED(framebuf.FrameBuffer)</c> after
/// <c>import adafruit_framebuf as framebuf</c>. Adafruit ssd1306 writes
/// that on <c>_SSD1306</c>.
///
/// WHAT DISCRIMINATES: prints 10.
/// </summary>
[TestFixture]
public class ImportedDottedSuperTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("imported-dotted-super"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("imported-dotted-super"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void SuperInit_OnAnAliasedImportedBase_PrintsTheBaseField()
    {
        FullRun(_session).Serial.Text.Should().Contain("10\nEND\n",
            because: "super().__init__(10) on framebuf.FrameBuffer stores width");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("10\nEND\n",
            because: "both front ends must resolve an imported dotted base the same way");
    }
}
