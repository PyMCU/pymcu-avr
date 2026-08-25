using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/pwm-duty-zero (PyMCU#147).
///
/// The AVR PWM HAL expressed duty 0 as OCR0A = 0 with the channel left in fast PWM
/// non-inverting mode. That is not off: in fast PWM the output is set at BOTTOM and
/// cleared on the compare match, so the compare register at BOTTOM never produces the
/// absence of output a caller means by duty 0. Off is COM0A1:0 cleared, which returns
/// the pin to normal port operation, plus the port bit driven low.
///
/// What is measured, and why it is measured this way:
///
///   The fixture reads TCCR0A, OCR0A and PORTD back THROUGH THE CPU into GPIORs and
///   then breaks. Watching the OC0A pin instead would be asking the simulation what it
///   thinks the waveform is; a HAL that writes the wrong registers is wrong whatever a
///   model then does with them, and the registers are what the HAL writes. The pin also
///   cannot be sampled after a break, because the CPU is halted there and the timer
///   stops with it.
///
///   The duty is seeded into GPIOR0, which this simulation retains. A literal duty
///   folds, and the folded path is already covered in the PyMCU repo by
///   tests/stdlib/test_pwm_duty_extremes.py; what needs a running chip is the branch a
///   run-time duty takes.
///
/// Measured on both HALs, phase 1 being the seeded duty and phase 2 a subsequent
/// set_duty(128). The unfixed HAL differs in exactly one cell, and it is the one this
/// issue is about:
///
///   seed          unfixed COM0A / OCR0A        fixed COM0A / OCR0A
///      0              10 / 0                       00 / 0
///     64              10 / 64                      10 / 64
///    128              10 / 128                     10 / 128
///    255              10 / 255                     10 / 255
///
/// Data-space addresses (ATmega328P):
///   GPIOR0 = 0x3E   GPIOR1 = 0x4A   GPIOR2 = 0x4B
/// </summary>
[TestFixture]
public class PwmDutyZeroTests
{
    private const int Gpior0Addr = 0x3E;   // seed in; PORTD read back out
    private const int Gpior1Addr = 0x4A;   // TCCR0A read back
    private const int Gpior2Addr = 0x4B;   // OCR0A read back

    private const int Oc0ABit = 6;         // OC0A is PD6 (Arduino D6)

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("pwm-duty-zero"));

    private readonly record struct Phase(byte Tccr0A, byte Ocr0A, byte PortD)
    {
        /// <summary>COM0A1:0, the two bits that connect the compare output to the pin.
        /// 0b10 is non-inverting fast PWM; 0b00 is the pin back under PORTD.</summary>
        public int Com0A => (Tccr0A >> 6) & 0b11;

        public bool PinDrivenHigh => (PortD & (1 << Oc0ABit)) != 0;
    }

    private static Phase Read(ArduinoUnoSimulation uno) =>
        new(uno.Data[Gpior1Addr], uno.Data[Gpior2Addr], uno.Data[Gpior0Addr]);

    /// <summary>Boots with `duty` seeded and returns both breakpoints: the seeded duty,
    /// then the same channel after a set_duty(128).</summary>
    private static (Phase Seeded, Phase Restored) Run(byte duty)
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = duty;

        uno.RunToBreak();
        var seeded = Read(uno);

        // RunToBreak parks ON the BREAK, so without stepping over it the second call
        // returns immediately and reports the first phase twice.
        uno.RunInstructions(1);
        uno.RunToBreak();
        var restored = Read(uno);

        return (seeded, restored);
    }

    // --- duty 0 is off ---------------------------------------------------------

    [Test]
    public void DutyZeroDisconnectsTheCompareOutput()
    {
        Run(0).Seeded.Com0A.Should().Be(0b00,
            "duty 0 must clear COM0A1:0 so the pin returns to normal port operation; " +
            "leaving it at 10 with OCR0A at BOTTOM is what kept the output driven");
    }

    [Test]
    public void DutyZeroDrivesThePinLowRatherThanLeavingItFloating()
    {
        Run(0).Seeded.PinDrivenHigh.Should().BeFalse(
            "once the compare output is disconnected, PORTD is what holds the pin");
    }

    [Test]
    public void DutyZeroIsNotExpressedAsACompareValue()
    {
        var seeded = Run(0).Seeded;
        seeded.Ocr0A.Should().Be(0);
        seeded.Com0A.Should().NotBe(0b10,
            "OCR0A = 0 with the output still connected is the bug, not the fix; " +
            "the compare register alone cannot express off");
    }

    // --- every non-zero duty still reaches the compare register -----------------

    [TestCase((byte)1)]
    [TestCase((byte)64)]
    [TestCase((byte)128)]
    [TestCase((byte)255)]
    public void ANonZeroDutyKeepsTheOutputConnectedAndWritesIt(byte duty)
    {
        var seeded = Run(duty).Seeded;
        seeded.Ocr0A.Should().Be(duty, "the duty must reach OCR0A unchanged");
        seeded.Com0A.Should().Be(0b10, "a non-zero duty is non-inverting fast PWM");
    }

    // --- and a channel that went off comes back --------------------------------

    [Test]
    public void AChannelThatDutyZeroSwitchedOffComesBackOnTheNextDuty()
    {
        var restored = Run(0).Restored;
        restored.Com0A.Should().Be(0b10,
            "set_duty(128) after a duty of 0 has to reconnect the compare output, " +
            "or the channel would stay dark for the rest of the program");
        restored.Ocr0A.Should().Be(128);
    }

    [Test]
    public void AChannelThatWasNeverOffIsUnaffectedByTheSameCall()
    {
        var restored = Run(200).Restored;
        restored.Com0A.Should().Be(0b10);
        restored.Ocr0A.Should().Be(128);
    }
}
