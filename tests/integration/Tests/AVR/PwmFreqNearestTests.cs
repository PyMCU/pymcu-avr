// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The PWM freq argument picks the nearest achievable frequency. Thresholds are
/// geometric midpoints between buckets: a 1000 Hz request on Timer1 lands on
/// 976.6 Hz (prescaler /64), not 7812.5 Hz as the old above-the-request policy
/// chose. Measured on a real Uno at 976.5 Hz with a logic analyser.
/// </summary>
[TestFixture]
public class PwmFreqNearestTests
{
    private SimSession _session = null!;

    private const int TCCR1A = 0x80;
    private const int TCCR1B = 0x81;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("pwm-freq-nearest"));

    [Test]
    public void Freq1000_SelectsPrescaler64_Mode5()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "F", maxMs: 100);
        var tccr1b = uno.Data[TCCR1B];
        (tccr1b & 0x07).Should().Be(0x03, "CS=011: prescaler /64 gives 976.6 Hz, nearest to the requested 1000 Hz");
        (tccr1b & 0x08).Should().Be(0x08, "WGM12 stays set (fast PWM 8-bit, mode 5)");
        var tccr1a = uno.Data[TCCR1A];
        (tccr1a & 0x83).Should().Be(0x81, "COM1A1 + WGM10: mode 5 on OC1A");
    }
}
