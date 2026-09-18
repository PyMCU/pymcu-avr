// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/type-checking-guard-import (PyMCU#480, #481): the nested
/// Adafruit TYPE_CHECKING guard keeps <c>PWMOut</c> from the try body
/// and does not load the <c>except NotImplementedError</c> stub.
///
/// WHAT DISCRIMINATES: prints 7.
/// </summary>
[TestFixture]
[Property("Issue", "480")]
public class TypeCheckingGuardImportTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("type-checking-guard-import"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("type-checking-guard-import"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void PwmOutFromTheTryBody_IsTheImportedClass()
    {
        FullRun(_session).Serial.Should().ContainLine("7",
            because: "except NotImplementedError must not drop PWMOut, so Thing(h: PWMOut) constructs and val() is 7");
    }

    [Test]
    [Property("Issue", "481")]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("7\nEND\n",
            because: "both front ends must keep the try-body PWMOut and not load the missing stub");
    }
}
