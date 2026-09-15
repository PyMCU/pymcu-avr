// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// PyMCU#441: a field first assigned from a property setter (adafruit_tcs34725's
/// integration_time.setter shape) or from a plain method __init__ calls directly
/// (adafruit_motor.servo's set_pulse_width_range shape) used to be invisible to
/// DeriveFieldLayout, which only scanned __init__ -- the build failed with
/// "'Sensor' has no field '...'". Runs the fixture on real AVR8Sharp silicon and checks
/// the printed values against what CPython 3.12, real MicroPython v1.21.0 and real
/// CircuitPython 9.2.1 all print for the equivalent program (measured, issue body):
/// raw=10, min_duty=11 (10 + 1, from the __init__-called helper), offset=5 (from the
/// setter).
/// </summary>
[TestFixture]
public class FieldFromSetterAndInitHelperTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("field-from-setter-and-init-helper"));

    [Test]
    public void FieldsFromASetterAndAnInitCalledHelperReadBackCorrectly()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "offset=5\n", maxMs: 3000);
        uno.Serial.Text.Should().Contain("raw=10\nmin_duty=11\noffset=5\n");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
