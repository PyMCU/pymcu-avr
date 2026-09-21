using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/sleep-ms-widths.
///
/// A constant time.sleep_ms()/sleep_us() lowers to a calibrated busy loop whose
/// counter is only as wide as the iteration count needs: 1, 2, 3 or 4 registers
/// at 3, 4, 5 or 6 cycles per iteration respectively. The fixture toggles pin 13
/// around each sleep and brackets it with BREAK checkpoints, so each pulse width
/// can be measured in cycles and each loop's register width read off the listing.
///
/// sleep_ms(500) at 16 MHz needs 8 000 000 cycles = 1 600 000 iterations of the
/// 5-cycle 3-register loop -- a fourth counter byte (LDI + SBCI, 4 bytes of flash
/// and a cycle per iteration) counted zero forever.
/// </summary>
[TestFixture]
public class SleepMsWidthTests
{
    private SimSession _session = null!;

    // (name, ideal cycles at 16 MHz, counter registers the loop must use)
    private static readonly (string What, long Cycles, int Regs)[] Steps =
    {
        ("sleep_ms(500)", 8_000_000, 3),
        ("sleep_ms(1)", 16_000, 2),
        ("sleep_ms(10)", 160_000, 2),
        ("sleep_ms(2000)", 32_000_000, 3),
        ("sleep_us(10)", 160, 1),
        ("sleep_ms(6000)", 96_000_000, 4),
    };

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("sleep-ms-widths"));

    [Test]
    public void EveryDelaySpinsForTheCyclesItAskedFor()
    {
        var uno = _session.Reset();
        foreach (var (what, cycles, _) in Steps)
        {
            uno.RunToBreak(maxInstructions: 150_000_000); // land on the BREAK before the sleep
            uno.RunInstructions(1);                       // step past it (PC stays on the break)
            var before = (long)uno.Cpu.Cycles;
            uno.RunToBreak(maxInstructions: 150_000_000); // land on the BREAK after the sleep
            var delta = (long)uno.Cpu.Cycles - before;
            uno.RunInstructions(1);                       // step past it for the next pair
            TestContext.Out.WriteLine($"{what}: {delta} cycles (ideal {cycles})");

            // The loop realizes floor(cycles / perIter) * perIter - 1 + regs setup
            // cycles, so it lands within a handful of cycles of the ask.
            delta.Should().BeInRange(cycles - 8, cycles + 8,
                $"{what} must spin for ~{cycles} cycles (got {delta})");
        }
    }

    [Test]
    public void EachLoopIsAsNarrowAsItsCountAllows()
    {
        var asm = File.ReadAllText(Path.Combine(
            PymcuCompiler.FixtureDir("sleep-ms-widths"), "dist", "debug", "firmware.asm"));
        var lines = asm.Split('\n').Select(l => l.Trim()).ToList();

        var loops = new List<List<string>>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].StartsWith("_dly_L") || !lines[i].EndsWith(":")) continue;
            var body = new List<string>();
            for (var j = i + 1; j < lines.Count && !lines[j].EndsWith(":"); j++)
                body.Add(lines[j]);
            loops.Add(body);
        }

        loops.Should().HaveCount(Steps.Length, "one inline calibrated loop per constant delay");
        for (var i = 0; i < Steps.Length; i++)
        {
            var (what, _, regs) = Steps[i];
            var sbci = loops[i].Count(l => l.StartsWith("SBCI"));
            sbci.Should().Be(regs - 1,
                $"{what} needs a {regs}-register counter, so its loop carries {regs - 1} SBCI");
        }
    }
}
