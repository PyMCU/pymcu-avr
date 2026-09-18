// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/classmethod-cv: <c>Mode.add_values((...))</c> populates class
/// attributes. Adafruit sht4x/tmp117 write that CV pattern.
///
/// WHAT DISCRIMINATES: prints 253, 1, 0, 1.
/// </summary>
[TestFixture]
public class ClassMethodCvTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("classmethod-cv"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("classmethod-cv"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void AddValues_BindsClassAttributesAndDicts()
    {
        FullRun(_session).Serial.Text.Should().Contain("253\n1\n0\n1\nEND\n",
            because: "setattr(cls, \"NOHEAT_HIGHPRECISION\", 0xFD) is 253; is_valid(0xFD) is 1; is_valid(0) is 0; delay[0xFD]*100 is 1");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("253\n1\n0\n1\nEND\n",
            because: "both front ends must expand @classmethod the same way");
    }
}
