// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/format-if-folds: <c>if buf_format == MVLSB: self.format = A()</c>
/// with an unused <c>B.rect</c> that forwards the buffer to
/// <c>set_pixel(framebuf, ...)</c>, reached through a subclass
/// <c>super().__init__(_FRAMEBUF_FORMAT)</c>. Adafruit framebuf
/// writes that if; ssd1306 forwards MVLSB that way.
///
/// WHAT DISCRIMINATES: prints 1 then 1.
/// </summary>
[TestFixture]
public class FormatIfFoldsTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("format-if-folds"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("format-if-folds"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void AConstFormatArg_PicksOnlyThatBranch()
    {
        FullRun(_session).Serial.Text.Should().Contain("1\n1\nEND\n",
            because: "super().__init__(_FRAMEBUF_FORMAT) must keep A() so pick and fill are 1");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("1\n1\nEND\n",
            because: "both front ends must fold the format if through super and skip unused B.rect");
    }
}
