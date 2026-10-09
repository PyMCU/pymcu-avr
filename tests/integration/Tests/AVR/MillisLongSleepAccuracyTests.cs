using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for fixtures/millis-long-sleep-accuracy.
///
/// millis_init() + sleep(1.0) + millis() around it must track the
/// SIMULATOR's own real elapsed cycles (uno.Cpu.Cycles / 16 MHz) to within
/// the 0.1% the task asks for -- not a hardcoded 1000: sleep()'s busy-wait
/// is cycle-exact for its OWN instructions, but Timer0's overflow ISR fires
/// ~977 times across that second and each firing steals real cycles the
/// delay loop does not know about, so the true wall-clock interval runs a
/// few ms past the nominal 1000. Counting only COMPLETE Timer0 overflows
/// also structurally lags true elapsed time by up to almost one overflow
/// period (~1.024 ms) on an isolated reading -- a completed-overflow-only
/// millis() under-reports by about that much even against the right ground
/// truth. millis() now folds in the in-progress overflow's TCNT0 plus the
/// ISR's carried fraction (the same correction micros() already applies)
/// to close that gap, so it should track uno.Cpu.Cycles tightly regardless
/// of how long the interval actually took.
/// </summary>
[TestFixture]
public class MillisLongSleepAccuracyTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("millis-long-sleep-accuracy"));

    [Test]
    public void ElapsedMillis_TracksRealCyclesWithinPointOnePercent()
    {
        var uno = _session.Reset();
        var startCycles = uno.Cpu.Cycles;
        uno.RunToBreak(maxInstructions: 200_000_000);
        var trueElapsedMs = (uno.Cpu.Cycles - startCycles) / 16000.0;

        var lo = uno.Data[Gpior0Addr];
        var hi = uno.Data[Gpior1Addr];
        int elapsed = lo | (hi << 8);

        var errorFraction = System.Math.Abs(elapsed - trueElapsedMs) / trueElapsedMs;
        errorFraction.Should().BeLessThan(0.001,
            $"millis() read {elapsed} against {trueElapsedMs:F3} true simulated ms " +
            "-- must be within 0.1%");
    }
}
