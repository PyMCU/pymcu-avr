// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/descriptor-self-on-imported-class: a <c>@property</c> on an
/// imported class reads <c>self.raw</c>, a descriptor. Adafruit
/// <c>INA219.bus_voltage</c> is this shape; the rewrite named the mangled
/// class key, which is not a bound variable.
///
/// WHAT DISCRIMINATES: <c>22</c>.
/// </summary>
[TestFixture]
public class DescriptorSelfOnImportedClassTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("descriptor-self-on-imported-class"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("descriptor-self-on-imported-class"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void APropertyThatReadsADescriptorOnAnImportedClass_PrintsTheGetAnswer()
    {
        FullRun(_session).Serial.Should().ContainLine("22",
            because: "self.raw inside Dev.volts is Field.__get__, so (9+2)*2 is 22");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("22\nEND\n",
            because: "both front ends must rewrite type(self).raw.__get__ using a name that exists");
    }
}
