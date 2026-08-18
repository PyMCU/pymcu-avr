// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Both channels of one timer must stay connected: pwm_init assigned the
/// shared TCCRxA absolutely, so initializing the B channel wiped the A
/// channel's COM bits - Arduino's analogWrite on D9+D10 together froze D9
/// on a real Uno. The COM bits are OR-ed in now.
/// </summary>
[TestFixture]
public class PwmDualChannelTests
{
    private SimSession _session = null!;

    private const int TCCR0A = 0x44;
    private const int TCCR1A = 0x80;
    private const int TCCR2A = 0xB0;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("pwm-dual-channel"));

    [Test]
    public void BothChannels_KeepTheirComBits()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "D", maxMs: 100);
        ((int)uno.Data[TCCR0A]).Should().Be(0xA3, "Timer0: COM0A1|COM0B1|WGM01|WGM00");
        ((int)uno.Data[TCCR1A]).Should().Be(0xA1, "Timer1: COM1A1|COM1B1|WGM10 (mode 5)");
        ((int)uno.Data[TCCR2A]).Should().Be(0xA3, "Timer2: COM2A1|COM2B1|WGM21|WGM20");
    }
}
