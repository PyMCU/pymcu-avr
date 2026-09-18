// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/rebound-module-alias (PyMCU#467):
/// <c>from motor import servo</c> then <c>servo = servo.Servo()</c>
/// then a field read and a method call. The Adafruit servo guide
/// spelling. Reads used to stay on the module alias
/// (<c>Unknown module member: motor_servo_x</c>).
///
/// WHAT DISCRIMINATES: <c>7</c>, <c>7</c>, END.
/// </summary>
[TestFixture]
[Property("Issue", "467")]
public class ReboundModuleAliasTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("rebound-module-alias"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("rebound-module-alias"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void AReadThroughTheReboundAliasPrintsTheField()
    {
        FullRun(_session).Serial.Text.Should().StartWith("7\n",
            because: "servo.x after servo = servo.Servo() is the instance field, not a module member");
    }

    [Test]
    public void AMethodCallThroughTheReboundAliasPrintsTheField()
    {
        FullRun(_session).Serial.Text.Should().Contain("7\n7\nEND\n",
            because: "servo.get() must not mangle to motor_servo_get as a free function");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("7\n7\nEND\n",
            because: "both front ends must rebind the imported submodule name to the instance");
    }
}
