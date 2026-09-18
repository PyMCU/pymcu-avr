// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/typing-extensions (PyMCU#462): <c>from typing_extensions import Protocol</c>
/// is a no-op, the same way <c>from typing import Protocol</c> already is (#444).
///
/// CircuitPython libraries write this unguarded for Python 3.7 -- adafruit_register
/// opens with it to declare <c>I2CDeviceDriver(Protocol)</c>. Before this,
/// DependencyGraphBuilder tried to LOAD typing_extensions like any third-party
/// module and refused with "Module not found: typing_extensions", so the fixture
/// did not build at all.
///
/// WHAT DISCRIMINATES: 7, the field Dev stores. Protocol is the structural-typing
/// base; Dev is the class that actually runs. A program that still refused the
/// import would not reach END.
/// </summary>
[TestFixture]
public class TypingExtensionsTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("typing-extensions"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("typing-extensions"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 1000);
        return uno;
    }

    [Test]
    public void AnUnguardedTypingExtensionsImport_BuildsAndTheMethodRuns()
    {
        FullRun(_session).Serial.Text.Should().Contain("7\nEND\n");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("7\nEND\n");
    }
}
