using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/delay-through-locals (PyMCU#327): a delay computed into locals costs what the same
/// arithmetic written out costs.
///
/// `nap` holds the microseconds and the milliseconds in locals and narrows with a cast;
/// `nap_spelled` writes the same arithmetic as one expression of the parameter. The second
/// folded through the AST evaluator and reached the calibrated busy loop; the first bound its
/// argument as a VARIABLE holding the constant, so `_delay_ms_avr` -- the generic counted
/// subroutine -- was what ran. Sixty bytes more, and 974 us where 1000 was asked for, for a
/// program whose only difference is a name.
/// </summary>
[TestFixture]
public class DelayThroughLocalsTests
{
    // delay_ms(1) at 16 MHz, the same window DelayOutlineTests uses.
    private const long MinCycles = 14_000;
    private const long MaxCycles = 20_000;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("delay-through-locals"));

    private static string Asm() => File.ReadAllText(Path.Combine(
        PymcuCompiler.FixtureDir("delay-through-locals"), "dist", "debug", "firmware.asm"));

    [Test]
    public void NeitherCallReachesTheGenericCountedDelay()
    {
        Asm().Should().NotContain("_delay_ms_avr",
            "a millisecond count known at compile time takes the calibrated loop, whichever way "
            + "the program spelled the arithmetic");
    }

    [Test]
    public void BothCallsAreACalibratedLoop()
    {
        var lines = Asm().Split('\n').Select(l => l.Trim()).ToList();
        int inline = lines.Count(l => l.StartsWith("_dly_L") && l.EndsWith(":"));
        int shared = lines.Count(l => l.StartsWith("CALL") && l.Contains("__dly_c"));
        (inline + shared).Should().Be(2, "one calibrated delay per call site");
    }

    [Test]
    public void TheTwoSpellingsSpinForTheSameTime()
    {
        var uno = _session.Reset();

        // main writes GPIOR0 then naps, so the cycles before the first BREAK include the
        // locals half and the cycles to the second BREAK the spelled-out one.
        var start = (long)uno.Cpu.Cycles;
        uno.RunToBreak(maxInstructions: 500_000);
        var viaLocals = (long)uno.Cpu.Cycles - start;

        uno.RunInstructions(1);
        var mid = (long)uno.Cpu.Cycles;
        uno.RunToBreak(maxInstructions: 500_000);
        var spelled = (long)uno.Cpu.Cycles - mid;

        viaLocals.Should().BeGreaterThanOrEqualTo(MinCycles).And.BeLessThanOrEqualTo(MaxCycles,
            "the half written with locals has to wait the millisecond it asked for");
        spelled.Should().BeGreaterThanOrEqualTo(MinCycles).And.BeLessThanOrEqualTo(MaxCycles);
        Math.Abs(viaLocals - spelled).Should().BeLessThan(200,
            "the two spellings are the same program and must take the same time");
    }
}
