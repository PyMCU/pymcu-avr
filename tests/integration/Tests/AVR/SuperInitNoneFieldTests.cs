// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/super-init-none-field: <c>super().__init__(reset=reset)</c>
/// with <c>reset</c> defaulting to None, then <c>if self.reset_pin:</c>.
/// Adafruit ssd1306 writes that on <c>_SSD1306</c>.
///
/// WHAT DISCRIMINATES: prints 1.
/// </summary>
[TestFixture]
public class SuperInitNoneFieldTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("super-init-none-field"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("super-init-none-field"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void SuperInit_ResetNone_PrintsTheElseField()
    {
        FullRun(_session).Serial.Text.Should().Contain("1\nEND\n",
            because: "if self.reset_pin: folds when super() forwarded None");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("1\nEND\n",
            because: "both front ends must fold a super-forwarded None the same way");
    }
}
