// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/classmethod-cv-imported: <c>Mode.add_values</c> in an imported
/// module, then <c>self._mode = Mode.NOHEAT_HIGHPRECISION</c> in the
/// constructor. Adafruit sht4x writes that shape.
///
/// WHAT DISCRIMINATES: prints 253.
/// </summary>
[TestFixture]
public class ClassMethodCvImportedTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("classmethod-cv-imported"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("classmethod-cv-imported"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void AddValues_OnAnImportedSubclass_BindsTheClassAttribute()
    {
        FullRun(_session).Serial.Text.Should().Contain("253\nEND\n",
            because: "setattr(cls, \"NOHEAT_HIGHPRECISION\", 0xFD) in an imported Mode is SHT._mode");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("253\nEND\n",
            because: "both front ends must bind an imported CV class attribute the same way");
    }
}
