// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/struct-calcsize-param: a class-body constructor calls
/// <c>_fit(struct.calcsize(struct_format))</c> with <c>struct_format: str</c>.
/// Adafruit <c>StructArray(0x06, "&lt;HH", 16)</c> is this shape; calcsize
/// refused a format the call site passed as a literal.
///
/// WHAT DISCRIMINATES: <c>7</c> stored at <c>_BUFFER[4]</c>.
/// </summary>
[TestFixture]
public class StructCalcsizeParamTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("struct-calcsize-param"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("struct-calcsize-param"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void IndexingPastTheDeclaredSize_ReadsTheGrownSlot()
    {
        FullRun(_session).Serial.Should().ContainLine("7",
            because: "calcsize(\"<HH\") through a str parameter is 4, so _fit grows _BUFFER to 5 and _BUFFER[4] stores 7");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("7\nEND\n",
            because: "both front ends must fold calcsize of a str format parameter");
    }
}
