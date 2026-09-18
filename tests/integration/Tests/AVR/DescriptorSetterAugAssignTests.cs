// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/descriptor-setter-augassign: descriptor <c>__set__</c> receives a
/// literal value, then does <c>value &lt;&lt;= self.shift</c> and
/// <c>obj.reg |= value</c> -- Adafruit <c>RWBits.__set__</c>, which is what
/// stopped adafruit_ina219 and adafruit_veml7700.
///
/// WHAT DISCRIMINATES: <c>48</c> (3 &lt;&lt; 4). A compile that still dropped
/// <c>value</c> after the shift would not build.
/// </summary>
[TestFixture]
public class DescriptorSetterAugAssignTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("descriptor-setter-augassign"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("descriptor-setter-augassign"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void ADescriptorWriteShiftsTheValueIntoTheRegister()
    {
        FullRun(_session).Serial.Should().ContainLine("48",
            because: "value <<= 4 must still be 3 after the shift so reg |= value stores 0x30");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("48\nEND\n",
            because: "both front ends must keep the setter's value after value <<= shift");
    }
}
