// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/const-for-tuple-field: <c>for cmd in (SET_DISP, 0x10 if
/// self.page else 0x00, self.height - 1)</c>. Adafruit ssd1306
/// init_display walks that tuple.
///
/// WHAT DISCRIMINATES: prints 237 (0xAE + 0 + 63).
/// </summary>
[TestFixture]
public class ConstForTupleFieldTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("const-for-tuple-field"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("const-for-tuple-field"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void ForOverNamedConstsAndAFieldTernary_PrintsTheSum()
    {
        FullRun(_session).Serial.Text.Should().Contain("237\nEND\n",
            because: "SET_DISP, a field ternary and self.height - 1 unroll as constants");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("237\nEND\n",
            because: "both front ends must fold those for-in elements the same way");
    }
}
