using System;
using AVR8Sharp.Core.Peripherals;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Regression test for PyMCU/PyMCU#407: <c>Pin.pulse_in()</c>'s measuring loop on the
/// ATmega48/88/168/328 family took 9 cycles per iteration, not the 8 its count-to-
/// microseconds conversion assumed (<c>count &gt;&gt; 1</c>), so every reading came back
/// about 11% short -- confirmed at a fixed 8/9 ratio across four independent distances
/// before the fix.
///
/// Drives <c>tests/integration/fixtures/pulse-in-hcsr04-cycle-count</c> (Trig D5/PD5, Echo
/// D2/PD2, top-level program, no <c>def main()</c>) with <see cref="HcSr04Simulator"/>,
/// which reports the true pulse width it drove independently of anything the firmware
/// measures, and checks the printed tenths-of-a-centimeter line against the same
/// <c>us * 17 // 100</c> conversion the firmware uses, computed from the true (rounded)
/// pulse width.
/// </summary>
[TestFixture]
public class PulseInHcsr04Tests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("pulse-in-hcsr04-cycle-count"));

    [TestCase(2.0)]
    [TestCase(9.7)]
    [TestCase(20.0)]
    [TestCase(40.0)]
    public void PulseIn_ReportsTruePulseWidth_WithinOneTenthOfACentimeter(double distanceCm)
    {
        var uno = _session.Reset();
        var sensor = new HcSr04Simulator(uno, uno.PortD, 5, uno.PortD, 2);

        double trueUs = sensor.Echo(distanceCm);

        uno.RunUntilSerial(uno.Serial, s => s.Contains('\n'), maxMs: 1000);
        int printed = int.Parse(uno.Serial.Text.Split('\n')[0]);

        // What CPython would compute from the same (rounded) true pulse width, using the
        // firmware's own conversion: tenths_cm = us * 17 // 100.
        long expected = (long)Math.Round(trueUs) * 17 / 100;

        Math.Abs(printed - expected).Should().BeLessOrEqualTo(1,
            $"true pulse={trueUs:F2} us, expected ~{expected} tenths-cm, firmware printed {printed}");
    }
}
