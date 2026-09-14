using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The CircuitPython PWM fade, written with range() (PyMCU#284).
///
/// `for p in range(0, 101)` up and `range(100, -1, -1)` down, each percentage scaled to the
/// 16-bit duty_cycle of pwmio.PWMOut. Both loops fit 8 bits, so they worked before the counter
/// was sized from its bounds; this pins that they keep working and that the duty reaches the
/// hardware: OCR0A holds the high byte of duty_cycle at the two BREAK checkpoints.
/// </summary>
[TestFixture]
public class RangePwmFadeTests
{
    private const int OCR0A = 0x47;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("range-pwm-fade"));

    [Test]
    public void ThePercentageRamp_PrintsWhatCPythonPrints()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 1500);
        uno.Serial.Text.Should().Contain(
            "u 0 0\nu 1 655\nu 50 32767\nu 99 64879\nu 100 65535\nd 100 65535\nd 50 32767\nd 0 0\nEND\n");
    }

    [Test]
    public void TheDutyReachesOCR0A_OnTheWayUp()
    {
        var uno = _session.Reset();
        uno.RunToBreak(maxInstructions: 2_000_000);
        uno.Data[OCR0A].Should().Be(252, "p == 99 is duty 64879: 253 of 256 counts high, OCR one less");
    }

    [Test]
    public void TheDutyReachesOCR0A_OnTheWayDown()
    {
        var uno = _session.Reset();
        uno.RunToBreak(maxInstructions: 2_000_000);
        uno.RunInstructions(1);
        uno.RunToBreak(maxInstructions: 2_000_000);
        uno.Data[OCR0A].Should().Be(127, "p == 50 is duty 32767: rounds to 128 counts, OCR 127");
    }
}
