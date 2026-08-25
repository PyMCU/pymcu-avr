using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/inline-multi-return (PyMCU#132).
///
/// An @inline expansion's result was tracked as the constant of its FIRST return, so a helper
/// that returns a different constant on each run-time branch folded to that first one. Every
/// consumer that folds a constant right-hand side then emitted nothing at all: the field store
/// was absent from the IR, and an @inline argument bound to the first return.
///
/// This is a VALUE claim, so it needs a seed the simulator retains. GPIOR0 (0x3E) and GPIOR1
/// (0x4A) carry the frequency, high byte then low, and they are written HERE rather than in
/// the firmware: a literal would fold the selecting branch away and measure the constant
/// folder instead. qemu does not retain a write to GPIOR0, which is why this lives in the
/// avr8sharp harness and not as a qemu boot check.
///
/// Against the unfixed compiler `direct` and `arg` read 1 for every seed and TCCR0B is 0x01
/// for every seed, which is the reported symptom: PWM.set_freq always programmed prescaler 1.
/// </summary>
[TestFixture]
public class InlineMultiReturnTests
{
    private const int Gpior0 = 0x3E;
    private const int Gpior1 = 0x4A;
    private const int Tccr0B = 0x45;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("inline-multi-return"));

    /// <summary>Runs the firmware with <paramref name="freq"/> seeded into GPIOR0:GPIOR1.</summary>
    private (string Serial, byte Tccr0B) RunWith(int freq)
    {
        var uno = _session.Reset();
        // Written after Reset and before the CPU runs, so the firmware's first read sees them.
        uno.Data[Gpior0] = (byte)(freq >> 8);
        uno.Data[Gpior1] = (byte)(freq & 0xFF);
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 2000);
        return (uno.Serial.Text, uno.Data[Tccr0B]);
    }

    // f, then what plain() returns for it, then the prescaler bits pwm_prescaler_for_freq
    // picks for PD6 (Timer0) at 16 MHz. 260 is worth keeping: plain says 5 where the HAL says
    // 0x04, so neither half can be mistaken for the other.
    private static readonly object[] Seeds =
    {
        new object[] { 60,    5, (byte)0x05 },
        new object[] { 260,   5, (byte)0x04 },
        new object[] { 4000,  2, (byte)0x02 },
        new object[] { 25600, 1, (byte)0x01 },
    };

    [TestCaseSource(nameof(Seeds))]
    public void FieldStoreRhs_IsTheValueTheHelperComputed(int freq, int expected, byte _)
    {
        RunWith(freq).Serial.Should().Contain($"direct={expected}\n",
            "a bare @inline call as a field-store right-hand side must emit its store");
    }

    [TestCaseSource(nameof(Seeds))]
    public void InlineArgument_IsTheValueTheHelperComputed(int freq, int expected, byte _)
    {
        RunWith(freq).Serial.Should().Contain($"arg={expected}\n",
            "a bare @inline call as another @inline's argument must pass its run-time value");
    }

    // The control. `plain(f) + 0` was already correct before the fix, so if this one ever goes
    // wrong the cause is somewhere other than the multi-return result.
    [TestCaseSource(nameof(Seeds))]
    public void ArithmeticConsumer_StaysCorrect(int freq, int expected, byte _)
    {
        RunWith(freq).Serial.Should().Contain($"expr={expected}\n");
    }

    // The reported symptom, in the register the hardware actually runs from.
    [TestCaseSource(nameof(Seeds))]
    public void SetFreq_ProgramsThePrescalerForThatFrequency(int freq, int _, byte expectedTccr0B)
    {
        RunWith(freq).Tccr0B.Should().Be(expectedTccr0B,
            "PWM.set_freq stores its computed prescaler through the same field-store shape");
    }

    /// <summary>
    /// EVERY SUITE THAT SEEDS NEEDS ONE OF THESE. Copy it when you write the next one.
    ///
    /// If the simulator does not retain the write to GPIOR0, the firmware reads zero, every
    /// seed produces the same run, and all sixteen assertions above pass VACUOUSLY: green, and
    /// measuring nothing. That failure is invisible, because a suite that measures nothing looks
    /// exactly like a suite that measures something and finds it correct.
    ///
    /// It is not hypothetical. qemu-system-avr does not retain a write to GPIOR0, so this same
    /// fixture under qemu would report plausible numbers descending from a seed that was never
    /// there. qemu is for "does it boot" and "did control flow take that branch", never for
    /// values.
    ///
    /// The guard is cheap: two different seeds must produce two different answers. It fails
    /// against the unfixed compiler too, for the other reason (every seed gave 0x01), which is
    /// a second thing worth knowing about the shape of this defect.
    /// </summary>
    [Test]
    public void TheSeedIsRetainedAndChangesTheAnswer()
    {
        RunWith(60).Tccr0B.Should().NotBe(RunWith(25600).Tccr0B,
            "if the simulator dropped the GPIOR write, every seed would produce the same run "
            + "and every assertion in this fixture would pass while measuring nothing");
    }
}
