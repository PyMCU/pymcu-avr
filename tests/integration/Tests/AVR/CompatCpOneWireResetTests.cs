using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The 1-Wire reset holds the line low for at least 480 us, measured in cycles.
///
/// PyMCU#501: `pymcu.time.delay_us` declared `us: uint8`, so `delay_us(480)` in
/// `pymcu_circuitpython.onewireio.OneWire.reset` was truncated to 224 and the pulse came out
/// less than half the length the protocol requires. Nothing caught it, because until this
/// fixture existed NOTHING in the suites compiled that module to firmware:
/// `surfacecov-ds18x20` is the only other thing that reaches it and it is build-refused on
/// purpose, so its green was the green of a refusal.
///
/// This asserts on the PULSE and not on the constant, deliberately. The constant can be
/// right and the pulse still come out short -- a miscalibrated loop, a body whose NOPs were
/// optimised away -- and an assertion on 480 would pass through all of it. What a DS18B20
/// can see is how long the line was low.
///
/// The master drives low by making the pin an output (its PORT bit is already clear) and
/// releases by making it an input again, so DDRD bit 2 is high for exactly the pulse.
///
/// Measured here, at 16 MHz: the pulse is 480.125 us and the release window 481.3125 us
/// against the 480 and 70+410 asked for. Before the fix they were 224.125 and 225.3125.
/// Those four numbers also answer a question the ROM gate could not: a loop count of 480
/// really does spend 480 us, so the truncated argument was the whole defect and the
/// calibration behind it is sound.
/// </summary>
[TestFixture]
public class CompatCpOneWireResetTests
{
    private const int DdrdAddr = 0x2A;
    private const int DataBit  = 2;         // board.D2 is PD2

    private const double UsPerCycle = 1.0 / 16.0;   // 16 MHz

    private static string _hex = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _hex = PymcuCompiler.BuildFixture("compat-cp-onewire-reset");

    /// <summary>Cycles the master held the line low, and cycles from release to the closing break.</summary>
    private static (long LowCycles, long ReleaseCycles) Reset()
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(_hex);
        uno.RunToBreak(1_000_000);          // the break before reset()

        bool Driving() => ((uno.Data[DdrdAddr] >> DataBit) & 1) == 1;

        long start = 0, released = 0;
        var driving = Driving();
        for (var i = 0; i < 2_000_000 && released == 0; i++)
        {
            uno.RunInstructions(1);
            var now = Driving();
            if (now == driving) continue;
            if (now) start = (long)uno.Cpu.Cycles;
            else released = (long)uno.Cpu.Cycles;
            driving = now;
        }

        start.Should().BeGreaterThan(0, "the master must drive the line low to reset the bus");
        released.Should().BeGreaterThan(start, "the master must release the line again");

        var afterRelease = (long)uno.Cpu.Cycles;
        uno.RunToBreak(2_000_000);          // the break after reset()
        return (released - start, (long)uno.Cpu.Cycles - afterRelease);
    }

    [Test]
    public void TheResetPulseIsAtLeastTheProtocolMinimum()
    {
        var (low, _) = Reset();

        // 480 us at 16 MHz is 7680 cycles. The minimum is a floor with no upper bound in the
        // protocol, so this only refuses a pulse that is too SHORT; the call overhead and the
        // loop's own rounding can only make it longer.
        (low * UsPerCycle).Should().BeGreaterThanOrEqualTo(480,
            "the 1-Wire reset pulse is at least 480 us -- before PyMCU#501 this measured about "
            + "224 us, because delay_us(480) was truncated to a byte");
    }

    [Test]
    public void TheReleaseWindowCompletesTheRecovery()
    {
        var (_, release) = Reset();

        // reset() samples for a presence pulse ~70 us after releasing and then waits 410 us
        // more. Both were truncated too: 70 fitted in a byte, 410 did not and became 154.
        (release * UsPerCycle).Should().BeGreaterThanOrEqualTo(480,
            "the line is left alone for 70 + 410 us after the pulse");
    }
}
