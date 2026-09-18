// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/descriptor-obj-type (PyMCU#419): descriptor <c>__get__</c>/<c>__set__</c>
/// receive <c>obj</c> as the owning instance, even when the parameter is annotated
/// with a typing-only placeholder (<c>I2CDeviceDriver</c>).
///
/// WHAT DISCRIMINATES: <c>11</c> (9 + 2 through __get__) and <c>5</c> (obj.base
/// after __set__). A compile that still treated obj as I2CDeviceDriver would not
/// build.
/// </summary>
[TestFixture]
[Property("Issue", "419")]
public class DescriptorObjTypeTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("descriptor-obj-type"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("descriptor-obj-type"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void ADescriptorReadAddsTheOwningInstancesField()
    {
        FullRun(_session).Serial.Should().ContainLine("11",
            because: "obj is the Dev instance, so obj.base is 2 and 9+2 is what CPython prints");
    }

    [Test]
    public void ADescriptorWriteReachesTheOwningInstance()
    {
        FullRun(_session).Serial.Should().ContainLine("5",
            because: "__set__ must write through obj.base, not refuse obj as I2CDeviceDriver");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("11\n5\nEND\n",
            because: "both front ends must substitute the owning instance for the typing-only obj");
    }
}
