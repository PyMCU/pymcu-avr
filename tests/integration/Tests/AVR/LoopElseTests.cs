using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/loop-else.
///
/// `for ... else` and `while ... else` run the else clause only when the loop finished
/// WITHOUT executing a break. Both forms used to be rejected with advice ("move the else body
/// to after the loop") that compiles into a different program: the else body then runs on
/// every path, including the one the break took. These checkpoints pin the distinction the
/// advice lost — the same loop with and without a break must land on different values.
///
/// Data-space addresses used: GPIOR0 = 0x3E, GPIOR1 = 0x4A, GPIOR2 = 0x4B
///
/// Checkpoints:
///   1 — for/else, break taken       → 1 (else skipped)
///   2 — for/else, no break          → 2 (else runs)
///   3 — while/else, break taken     → 1
///   4 — while/else, no break        → 2
///   5 — inner break, outer else     → 7 (a nested loop owns its own break)
///   6 — continue only               → 9 (continue is not a break)
///   7 — break out of a try/finally  → 1, and the finally ran 4 times
/// </summary>
[TestFixture]
public class LoopElseTests
{
    private SimSession _session = null!;

    // ATmega328P data-space addresses
    private const int GPIOR1_ADDR = 0x4A;
    private const int GPIOR2_ADDR = 0x4B;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("loop-else"));

    /// <summary>Advances the simulation through N BREAK checkpoints.</summary>
    private static void SkipBreaks(ArduinoUnoSimulation uno, int count)
    {
        for (var i = 0; i < count; i++)
        {
            uno.RunToBreak();
            uno.RunInstructions(1); // step over the BREAK opcode
        }
    }

    private ArduinoUnoSimulation Boot() => _session.Reset();

    /// <summary>Runs to checkpoint <paramref name="n"/> (1-based) and returns the board.</summary>
    private ArduinoUnoSimulation AtCheckpoint(int n)
    {
        var uno = Boot();
        SkipBreaks(uno, n - 1);
        uno.RunToBreak();
        return uno;
    }

    [Test]
    public void ForElse_BreakTaken_ElseBodyIsSkipped()
    {
        var uno = AtCheckpoint(1);
        uno.Data[GPIOR1_ADDR].Should().Be(1,
            "the search found its value and broke out, so the else clause must not run — " +
            "2 here would mean the else body ran on the break path");
    }

    [Test]
    public void ForElse_NoBreak_ElseBodyRuns()
    {
        var uno = AtCheckpoint(2);
        uno.Data[GPIOR1_ADDR].Should().Be(2,
            "the loop ran to the end of the range without breaking, so the else clause runs");
    }

    [Test]
    public void WhileElse_BreakTaken_ElseBodyIsSkipped()
    {
        var uno = AtCheckpoint(3);
        uno.Data[GPIOR1_ADDR].Should().Be(1,
            "the while loop broke out, so its else clause must not run");
    }

    [Test]
    public void WhileElse_ConditionWentFalse_ElseBodyRuns()
    {
        var uno = AtCheckpoint(4);
        uno.Data[GPIOR1_ADDR].Should().Be(2,
            "the while condition went false with no break, so the else clause runs");
    }

    [Test]
    public void NestedLoop_InnerBreak_DoesNotCancelOuterElse()
    {
        var uno = AtCheckpoint(5);
        uno.Data[GPIOR1_ADDR].Should().Be(7,
            "the break belongs to the inner loop; the outer loop never broke, so its else runs");
    }

    [Test]
    public void ContinueOnly_ElseBodyRuns()
    {
        var uno = AtCheckpoint(6);
        uno.Data[GPIOR1_ADDR].Should().Be(9,
            "continue is not a break — a loop that only continues still finishes normally");
    }

    [Test]
    public void BreakOutOfTry_SkipsElse_AndStillRunsFinally()
    {
        var uno = AtCheckpoint(7);
        uno.Data[GPIOR1_ADDR].Should().Be(1,
            "the break inside the try still counts as breaking out of the loop");
        uno.Data[GPIOR2_ADDR].Should().Be(4,
            "the finally block runs on each of iterations 0..3, the last of them on the way out");
    }
}
