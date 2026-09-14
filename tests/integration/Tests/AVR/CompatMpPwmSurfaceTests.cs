using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The machine.PWM spellings MicroPython programs use (pymcu-micropython#6): duty() on the
/// legacy 0..1023 scale, duty_ns() and the duty_ns= keyword, init(freq=, duty_u16=), invert=1,
/// and the getters after a runtime set. Before this duty(512) arrived as a uint8 and switched
/// the output off, the other three spellings were refused, and duty_u16(runtime) read back 0.
/// </summary>
[TestFixture]
public class CompatMpPwmSurfaceTests
{
    private const int TCCR0A = 0x44;
    private const int TCCR0B = 0x45;
    private const int OCR0A = 0x47;
    private const int OCR2B = 0xB4;   // Pin(3) is Timer2's OC2B: Timer0's other channel would collide with init(freq=20000)

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("compat-mp-pwm-surface"));

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
    public void Invert_ProgramsTheInvertingMode()
    {
        var uno = Checkpoint(1);
        uno.Data[TCCR0A].Should().Be(0xC3);
        uno.Data[OCR0A].Should().Be(128);
    }

    [Test]
    public void Duty_IsTheLegacy1023Scale()
        => Checkpoint(2).Data[OCR0A].Should().Be(128, "duty(512) is 50 %");

    [Test]
    public void DutyNs_DerivesFromTheFrequency()
        => Checkpoint(3).Data[OCR0A].Should().Be(64, "250 us at 1 kHz is 25 %");

    [Test]
    public void InitKeywords_ReprogramFrequencyAndDuty()
    {
        var uno = Checkpoint(4);
        uno.Data[TCCR0B].Should().Be(0x02, "20 kHz is prescaler 8");
        uno.Data[OCR0A].Should().Be(192);
    }

    [Test]
    public void DutyNsKeyword_AtConstruction()
        => Checkpoint(5).Data[OCR2B].Should().Be(64, "125 us at 2 kHz is 25 %");

    [Test]
    public void TheGetters_ReadBackWhatWasSet()
    {
        var uno = Checkpoint(6);
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 300);
        uno.Serial.Text.Should().Contain("512\n16384\n8192\n");
    }
}
