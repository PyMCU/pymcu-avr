using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/avr/delay-outline.
///
/// A delay_ms/delay_us call with a constant argument lowers to a calibrated busy
/// loop. Emitted inline that loop costs 18 bytes at every call site; when the same
/// iteration count is reached from two or more sites the loop becomes a shared
/// parameterless subroutine (__dly_c&lt;loops&gt;) and each site is a 4-byte CALL.
///
/// The subroutine runs one iteration less than the inline form, so the 8 cycles of
/// CALL+RET stand in for the 6 cycles of the dropped iteration: delay_ms(1) at 16 MHz
/// measures 16 001 cycles outlined against the 16 000 of the inline loop — longer,
/// never shorter. These tests pin both halves: the structure (one shared body, no
/// inline loop left) and the timing.
/// </summary>
[TestFixture]
public class DelayOutlineTests
{
    private SimSession _session = null!;

    // delay_ms(1) at 16 MHz. Same window as CycleTimingTests: generous enough to
    // survive loop-count tweaks, tight enough to catch a delay that stopped waiting.
    private const long MinCycles = 14_000;
    private const long MaxCycles = 20_000;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("delay-outline"));

    [Test]
    public void OutlinedDelay_StillSpins_ForOneMillisecond()
    {
        var uno = _session.Reset();
        uno.RunToBreak();                           // checkpoint 1
        var before = (long)uno.Cpu.Cycles;
        uno.RunInstructions(1);                     // step past BREAK 1
        uno.RunToBreak(maxInstructions: 500_000);   // checkpoint 2
        var delta = (long)uno.Cpu.Cycles - before;

        delta.Should().BeGreaterThanOrEqualTo(MinCycles).And
             .BeLessThanOrEqualTo(MaxCycles,
                 "the shared delay subroutine must spin for the same ~16 000 cycles as the inline loop");
    }

    [Test]
    public void RepeatedDelay_IsOneSharedSubroutine_NotAnInlineLoopPerSite()
    {
        var asm = File.ReadAllText(Path.Combine(
            PymcuCompiler.FixtureDir("delay-outline"), "dist", "debug", "firmware.asm"));
        var lines = asm.Split('\n').Select(l => l.Trim()).ToList();

        lines.Count(l => l.StartsWith("__dly_c") && l.EndsWith(":"))
             .Should().Be(1, "the three delay_ms(1) sites share a single loop body");
        lines.Count(l => l.StartsWith("CALL") && l.Contains("__dly_c"))
             .Should().Be(3, "each site is a CALL to that shared body");
        lines.Count(l => l.StartsWith("_dly_L") && l.EndsWith(":"))
             .Should().Be(0, "no inline busy loop is left behind");
    }
}
