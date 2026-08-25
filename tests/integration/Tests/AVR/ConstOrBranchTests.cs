using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/const-or-branch (PyMCU#153).
///
/// One of the eight static combinations of `and` / `or` between two compile-time comparisons
/// used to leave a jump to a label nobody defined: `X or Y` with X true and Y false. The left
/// operand folds true and jumps over the rest, the right folds false and jumps to the caller's
/// else label, and together they answer "statically true", so the caller keeps only the then
/// branch and never defines that label. The jump is unreachable, but an undefined label is
/// `ld: undefined reference to L_2` -- and only PYMCU_NO_OPT=1 showed it, because the
/// optimizer deletes the jump before the linker can miss the label.
///
/// The fixture is in the differential corpus, so building it unoptimized is itself the
/// regression test for the link failure. These checkpoints add the other half: which branch
/// each combination takes, because folding the wrong way would link perfectly.
///
/// Data-space addresses used: GPIOR0 = 0x3E, GPIOR1 = 0x4A, GPIOR2 = 0x4B
/// </summary>
[TestFixture]
public class ConstOrBranchTests
{
    private SimSession _session = null!;

    private const int GPIOR0_ADDR = 0x3E;
    private const int GPIOR1_ADDR = 0x4A;
    private const int GPIOR2_ADDR = 0x4B;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("const-or-branch"));

    private static void SkipBreaks(ArduinoUnoSimulation uno, int count)
    {
        for (var i = 0; i < count; i++)
        {
            uno.RunToBreak();
            uno.RunInstructions(1); // step over the BREAK opcode
        }
    }

    private ArduinoUnoSimulation AtCheckpoint(int n)
    {
        var uno = _session.Reset();
        SkipBreaks(uno, n - 1);
        uno.RunToBreak();
        return uno;
    }

    [Test]
    public void TheTruthTableOfOr_IsTakenCorrectly()
    {
        // bits 0..3 = or(T,T) or(T,F) or(F,T) or(F,F) -> 1 1 1 0
        var uno = AtCheckpoint(1);
        (uno.Data[GPIOR0_ADDR] & 0x0F).Should().Be(0x07,
            "an `or` is true unless BOTH sides are false; bit 1 is the combination that used " +
            "not to link");
    }

    [Test]
    public void TheTruthTableOfAnd_IsTakenCorrectly()
    {
        // bits 4..7 = and(T,T) and(T,F) and(F,T) and(F,F) -> 1 0 0 0
        var uno = AtCheckpoint(1);
        (uno.Data[GPIOR0_ADDR] & 0xF0).Should().Be(0x10,
            "an `and` is true only when both sides are");
    }

    [Test]
    public void EveryCombinationTakesExactlyOneBranch()
    {
        // The else mask has to be the exact complement of the then mask: never both arms,
        // never neither. Defining an abandoned label must not resurrect the branch it belonged
        // to, which is the way this fix could have gone wrong.
        var uno = AtCheckpoint(1);
        var then = uno.Data[GPIOR0_ADDR];
        var otherwise = uno.Data[GPIOR1_ADDR];

        (then ^ otherwise).Should().Be(0xFF,
            $"then mask 0x{then:X2} and else mask 0x{otherwise:X2} must partition the eight cases");
    }

    [Test]
    public void TheReportedDispatch_ReachesEachArm()
    {
        // arm("PD2") goes through the `or` that used to dangle, arm(3) through the elif's,
        // and arm("PB0") past both.
        var uno = AtCheckpoint(2);

        uno.Data[GPIOR0_ADDR].Should().Be(1, "\"PD2\" matches the first arm");
        uno.Data[GPIOR1_ADDR].Should().Be(2, "3 matches the second arm by its board number");
        uno.Data[GPIOR2_ADDR].Should().Be(0, "\"PB0\" matches neither");
    }
}
