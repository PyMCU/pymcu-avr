using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The inverting compare output mode of the AVR PWM HAL (PyMCU#293): `invert=1` programs
/// COMxn1:COMxn0 = 11 on the channel's own pair of bits, off still disconnects, and a
/// non-zero duty reconnects inverting. The non-inverting programs are byte-identical to
/// before, which the rest of the suite guards.
/// </summary>
[TestFixture]
public class PwmInvertTests
{
    private const int TCCR0A = 0x44;
    private const int OCR0A = 0x47;
    private const int OCR0B = 0x48;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("pwm-invert"));

    private ArduinoUnoSimulation Checkpoint(int n)
    {
        var uno = _session.Reset();
        for (int i = 0; i < n; i++)
        {
            if (i > 0) uno.RunInstructions(1);
            uno.RunToBreak();
        }
        return uno;
    }

    [Test]
    public void InvertingChannelA_SetsBothComBits()
    {
        var uno = Checkpoint(1);
        uno.Data[TCCR0A].Should().Be(0xC3);
        uno.Data[OCR0A].Should().Be(128);
    }

    [Test]
    public void DutyZero_StillDisconnects()
        => Checkpoint(2).Data[TCCR0A].Should().Be(0x03);

    [Test]
    public void ANonZeroDuty_ReconnectsInverting()
    {
        var uno = Checkpoint(3);
        uno.Data[TCCR0A].Should().Be(0xC3);
        uno.Data[OCR0A].Should().Be(64);
    }

    [Test]
    public void InvertingChannelB_LeavesChannelAAlone()
    {
        var uno = Checkpoint(4);
        uno.Data[TCCR0A].Should().Be(0xF3, "both channels inverting: COM0A = 11 and COM0B = 11");
        uno.Data[OCR0B].Should().Be(200);
    }
}
