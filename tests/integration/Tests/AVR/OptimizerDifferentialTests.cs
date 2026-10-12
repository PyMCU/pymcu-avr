using System.Text.RegularExpressions;
using NUnit.Framework;
using PyMCU.IntegrationTests.Differential;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Differential test of the IR optimizer: every program in the repository is compiled
/// twice — once normally and once with <c>PYMCU_NO_OPT=1</c>, which makes
/// <c>IrGenerationPhase</c> hand the raw IR straight to the backend — and both images are
/// run on the same simulated Arduino Uno. Whatever the program is supposed to do, the two
/// builds must do the same thing: the same bytes out of the UART, in the same order, and
/// the same sequence of levels on the pins.
///
/// A difference is by construction a bug in the optimizer, and it is exactly the shape of
/// bug this project keeps finding the hard way: a peephole that forwarded a load it should
/// not have, a register-write model that mismodelled pointer pairs, an @inline expansion
/// mis-grouped by the outliner. All of them compiled, ran, and produced wrong answers
/// silently. Comparing against the unoptimized build turns that class of bug from something
/// found by luck into something found by running the suite.
///
/// Scope: this covers the IR optimizer (<c>Optimizer.Optimize</c>), which is what
/// <c>PYMCU_NO_OPT</c> gates. The AVR peephole runs in both builds and so cancels out here;
/// it has its own axis in <see cref="PeepholeDifferentialTests"/>, gated by
/// <c>PYMCU_NO_PEEPHOLE</c>.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
[Category("Differential")]
public class OptimizerDifferentialTests
{
    private static IEnumerable<TestCaseData> Corpus() =>
        DifferentialCorpus.All().Select(p => new TestCaseData(p).SetName($"Differential({p})"));

    // avr-ld's answer when a section does not fit the chip's flash. Only the unoptimized
    // build is allowed to produce it: it is bigger than the optimized one by construction,
    // so a program can be a valid differential case and still have no image to compare with.
    private static readonly Regex FlashOverflow = new(@"avr-ld: .*will not fit in region");

    internal static bool IsFlashOverflow(Exception e) => FlashOverflow.IsMatch(e.Message);

    // The unoptimized image, or null when that build overflows the flash. Any other failure
    // of the unoptimized build propagates and fails the test.
    private static string? UnoptimizedOrNull(DiffProgram program)
    {
        try
        {
            return program.Unoptimized();
        }
        catch (InvalidOperationException e) when (IsFlashOverflow(e))
        {
            return null;
        }
    }

    // The message PymcuCompiler throws for a failed build, with the text avr-ld printed on
    // the real program that overflowed (adafruit-bmp280-unmodified-loop, noopt build).
    [Test]
    public void UnoptimizedFlashOverflow_IsRecognised()
    {
        const string message =
            "pymcu build failed for 'adafruit-bmp280-unmodified-loop (noopt)' (exit 1):\n" +
            "    Linking... ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━           0:00:04\n" +
            "Assembly Error: avr-ld failed:\n" +
            "avr-ld: /work/firmware.elf section `.text' will not fit in region `flash'\n";

        Assert.That(IsFlashOverflow(new InvalidOperationException(message)), Is.True);
    }

    // A failed build that is not avr-ld's overflow must still fail the test: an error from
    // the compiler, or an undefined symbol at link time, is a bug and not a size limit.
    [TestCase("pymcu build failed for 'x (noopt)' (exit 1):\nerror: CS1002: ; expected")]
    [TestCase("pymcu build failed for 'x (noopt)' (exit 1):\navr-ld: undefined reference to `L_2'")]
    public void OtherUnoptimizedBuildFailures_StillFail(string message)
        => Assert.That(IsFlashOverflow(new InvalidOperationException(message)), Is.False);

    [TestCaseSource(nameof(Corpus))]
    public void OptimizedBuild_BehavesLikeUnoptimizedBuild(DiffProgram program)
    {
        var optimized = BehaviorRecorder.Record(program.Optimized(), TraceBudget.Default);

        var unoptimizedHex = UnoptimizedOrNull(program);
        // The optimized build still had to build above; only the comparison is skipped.
        if (unoptimizedHex is null)
            Assert.Ignore(
                $"{program}: the unoptimized build (PYMCU_NO_OPT=1) does not fit the chip's " +
                "flash, so there is no unoptimized image to compare the optimized build with. " +
                "avr-ld reports the .text section will not fit in region flash; the byte count " +
                "it needs is not in that message.");

        var unoptimized = BehaviorRecorder.Record(unoptimizedHex, TraceBudget.Default);

        var difference = TraceComparison.FirstDifference(optimized, unoptimized);
        var known = DifferentialCorpus.KnownDivergences.GetValueOrDefault(program.ToString());

        if (difference != null)
        {
            var report =
                $"{program}: optimized and unoptimized builds behave differently.\n" +
                $"{difference}\n" +
                $"  ({TraceComparison.Summarize(optimized, unoptimized)})\n" +
                $"  reproduce: build the project twice, the second time with PYMCU_NO_OPT=1.";

            if (known == null) Assert.Fail(report);
            Assert.Inconclusive($"Known divergence — {known}.\n{report}");
        }

        // A known divergence that stopped diverging means the underlying bug is gone. Fail so
        // the entry is deleted with the fix instead of quietly outliving it.
        if (known != null)
            Assert.Fail(
                $"{program} no longer diverges (was: {known}). Remove it from " +
                $"{nameof(DifferentialCorpus)}.{nameof(DifferentialCorpus.KnownDivergences)}.");

        // A program that neither talks nor moves a pin proves nothing either way. Say so
        // rather than passing quietly, so the corpus does not silently rot into no-ops.
        if (optimized.IsSilent && unoptimized.IsSilent)
            Assert.Inconclusive(
                $"{program}: no observable behaviour within {TraceBudget.Default.MaxMs} ms of " +
                "simulated time — nothing was compared. The program most likely waits on a " +
                "peripheral or on UART input that this harness does not provide.");
    }
}
