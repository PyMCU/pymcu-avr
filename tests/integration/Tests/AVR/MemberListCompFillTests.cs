// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/member-listcomp-fill: <c>self.buf = [color for i in
/// range(len(self.buf))]</c>. Adafruit GS2HMSBFormat.fill writes
/// that assignment to <c>framebuf.buf</c>.
///
/// WHAT DISCRIMINATES: prints 9 then 9.
/// </summary>
[TestFixture]
public class MemberListCompFillTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("member-listcomp-fill"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("member-listcomp-fill"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void AListCompAssignedToAFieldArray_PrintsTheFill()
    {
        FullRun(_session).Serial.Text.Should().Contain("9\n9\nEND\n",
            because: "self.buf = [color for i in range(len(self.buf))] writes color into every slot");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("9\n9\nEND\n",
            because: "both front ends must fill a field array from that comprehension");
    }
}
