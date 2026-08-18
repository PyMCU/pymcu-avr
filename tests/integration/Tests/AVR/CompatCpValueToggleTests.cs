// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Reading digitalio's .value calls the property getter, so it reports the pin.
/// The name collides with the ptr[T] dereference (`p.value`), which used to claim
/// every member spelled that way and hand back the receiver — making the getter
/// report the instance and `led.value = not led.value` a no-op on hardware.
/// </summary>
[TestFixture]
public class CompatCpValueToggleTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-value-toggle"));

    [Test]
    public void ValueReadsThePin_AndTogglingFlipsIt()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "d=1\n", maxMs: 3000);
        uno.Serial.Text.Should().Contain("a=1\nb=0\nc=1\nd=1\n");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
