using NUnit.Framework;
using PyMCU.IntegrationTests.Differential;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Differential test of the AVR backend peephole: every program in the repository is
/// compiled twice from the <b>same IR</b> — once normally and once with
/// <c>PYMCU_NO_PEEPHOLE=1</c>, which makes <c>AvrPeephole.Optimize</c> hand the assembly
/// straight through — and both images are run on the same simulated Arduino Uno. Whatever
/// the program is supposed to do, the two builds must do the same thing: the same bytes out
/// of the UART, in the same order, and the same sequence of levels on the pins.
///
/// This is the axis <see cref="OptimizerDifferentialTests"/> is blind to. <c>PYMCU_NO_OPT</c>
/// gates only the IR optimizer, so the peephole runs in both of its builds and its mistakes
/// cancel out — and the peephole is where this project's most expensive bugs have lived: an
/// absolute STS/LDS forwarding that was not valid, and a register-write model that
/// mismodelled the pointer pairs. Both compiled, ran, and produced wrong answers silently;
/// one was only caught on a physical Arduino.
///
/// The IR optimizer stays <b>on in both builds</b>, deliberately. Turning it off would feed
/// the backend un-outlined IR and bring in <c>AvrCodeGen</c>'s own inline-expansion outliner,
/// a separate code path with 27 catalogued divergences of its own
/// (<see cref="DifferentialCorpus.KnownDivergences"/>) that would be impossible to tell apart
/// from a peephole bug. Here, the two builds differ in nothing but this one pass, so a
/// divergence is a peephole bug by construction.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
[Category("Differential")]
public class PeepholeDifferentialTests
{
    private static IEnumerable<TestCaseData> Corpus() =>
        DifferentialCorpus.All().Select(p => new TestCaseData(p).SetName($"Peephole({p})"));

    [TestCaseSource(nameof(Corpus))]
    public void PeepholedBuild_BehavesLikeUnpeepholedBuild(DiffProgram program)
    {
        var peepholed = BehaviorRecorder.Record(program.Optimized(),  TraceBudget.Default);
        var plain     = BehaviorRecorder.Record(program.NoPeephole(), TraceBudget.Default);

        var difference = TraceComparison.FirstDifference(peepholed, plain, TraceLabels.Peephole);
        var known = DifferentialCorpus.KnownPeepholeDivergences.GetValueOrDefault(program.ToString());

        if (difference != null)
        {
            var report =
                $"{program}: the peephole changed what the program does.\n" +
                $"{difference}\n" +
                $"  ({TraceComparison.Summarize(peepholed, plain, TraceLabels.Peephole)})\n" +
                $"  reproduce: build the project twice, the second time with PYMCU_NO_PEEPHOLE=1,\n" +
                $"  and diff the two dist/debug/firmware.asm files.";

            if (known == null) Assert.Fail(report);
            Assert.Inconclusive($"Known peephole divergence — {known}.\n{report}");
        }

        // A known divergence that stopped diverging means the underlying bug is gone. Fail so
        // the entry is deleted with the fix instead of quietly outliving it.
        if (known != null)
            Assert.Fail(
                $"{program} no longer diverges (was: {known}). Remove it from " +
                $"{nameof(DifferentialCorpus)}.{nameof(DifferentialCorpus.KnownPeepholeDivergences)}.");

        // A program that neither talks nor moves a pin proves nothing either way. Say so
        // rather than passing quietly, so the corpus does not silently rot into no-ops.
        if (peepholed.IsSilent && plain.IsSilent)
            Assert.Inconclusive(
                $"{program}: no observable behaviour within {TraceBudget.Default.MaxMs} ms of " +
                "simulated time — nothing was compared. The program most likely waits on a " +
                "peripheral or on UART input that this harness does not provide.");
    }

    /// <summary>
    /// Guards the harness itself. Every assertion above is vacuous if <c>PYMCU_NO_PEEPHOLE=1</c>
    /// never reaches <c>pymcuc-avr</c> — the two builds would be the same image and agree
    /// trivially, and the suite would report a healthy peephole while testing nothing. A
    /// program with enough code for the peephole to bite must produce a different image.
    /// </summary>
    [Test]
    public void PeepholeSwitch_ActuallyChangesTheEmittedImage()
    {
        var program = new DiffProgram(ProgramKind.Fixture, "print-integers");

        Assert.That(program.NoPeephole(), Is.Not.EqualTo(program.Optimized()),
            $"{program} compiled to an identical image with and without PYMCU_NO_PEEPHOLE=1. " +
            "The switch is not reaching the backend (stale pymcuc-avr binary, or the variable " +
            "is being stripped from the build environment), so the peephole axis is comparing " +
            "a build against itself.");
    }
}
