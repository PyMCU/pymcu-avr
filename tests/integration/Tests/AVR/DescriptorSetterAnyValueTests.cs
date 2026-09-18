// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/descriptor-setter-any-value: descriptor <c>__set__(..., value: Any)</c>
/// then <c>obj.reg = value</c>. Adafruit <c>UnaryStruct.__set__</c> is this
/// annotation; reading <c>value</c> was refused as having no width even though
/// the written value is an int.
///
/// WHAT DISCRIMINATES: <c>7</c>.
/// </summary>
[TestFixture]
public class DescriptorSetterAnyValueTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("descriptor-setter-any-value"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("descriptor-setter-any-value"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void ADescriptorWriteThroughAny_PrintsTheWrittenValue()
    {
        FullRun(_session).Serial.Should().ContainLine("7",
            because: "value: Any on __set__ is the written 7, not a missing width");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("7\nEND\n",
            because: "both front ends must treat Any on __set__ as the written value");
    }
}
