// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The PWM freq argument picks the nearest achievable frequency. Thresholds are
/// geometric midpoints between buckets: a 1000 Hz request on Timer0 lands on
/// 976.6 Hz (prescaler /64), not 7812.5 Hz as the old above-the-request policy
/// chose. Measured on a real Uno at 976.5 Hz with a logic analyser, on Timer1,
/// which is where this fixture used to run: Timer1 no longer buckets, because a
/// frequency that is not one of its five now reaches the mode whose period is a
/// register and comes out exactly (pymcu-circuitpython#8). Timer0 and Timer2 still
/// bucket, and this rule is still theirs.
/// </summary>
[TestFixture]
public class PwmFreqNearestTests
{
    private SimSession _session = null!;

    private const int TCCR0A = 0x44;
    private const int TCCR0B = 0x45;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("pwm-freq-nearest"));

    [Test]
    public void Freq1000_SelectsPrescaler64_FastPwm()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "F", maxMs: 100);
        var tccr0b = uno.Data[TCCR0B];
        (tccr0b & 0x07).Should().Be(0x03, "CS=011: prescaler /64 gives 976.6 Hz, nearest to the requested 1000 Hz");
        var tccr0a = uno.Data[TCCR0A];
        (tccr0a & 0x83).Should().Be(0x83, "COM0A1 + WGM01 + WGM00: fast PWM on OC0A");
    }
}
