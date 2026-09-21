using NUnit.Framework;
using PyMCU.IntegrationTests.Differential;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Differential test of the profile-guided optimisation path: every program in the
/// repository is compiled twice from the same sources, once plain, and once with a
/// profile collected by running the declared workload (or the default 200 ms idle
/// scenario) on the emulator, fed back through <c>pymcuc --profile</c>. Whatever the
/// program is supposed to do, the two builds must do the same thing: the same bytes
/// out of the UART, in the same order, the same sequence of levels on the pins, and
/// the same BREAK checkpoints.
///
/// The profile is allowed to change *code*: it moves cold inline-expansion regions
/// out of line and keeps hot ones inline, but never *behaviour*. A divergence here
/// is an optimizer miscompile by construction, the same contract as
/// <see cref="OptimizerDifferentialTests"/>.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
[Category("Differential")]
public class PgoDifferentialTests
{
    private static IEnumerable<TestCaseData> Corpus() =>
        DifferentialCorpus.All().Select(p => new TestCaseData(p).SetName($"Pgo({p})"));

    [TestCaseSource(nameof(Corpus))]
    public void ProfiledBuild_BehavesLikePlainBuild(DiffProgram program)
    {
        if (!File.Exists(PymcuCompiler.ProfilerBinary))
            Assert.Ignore("pymcuc-avr-profiler not built (build/bin-profiler/); " +
                          "the PGO axis needs the self-contained profiler binary.");

        string profiled;
        try
        {
            profiled = program.Kind == ProgramKind.Example
                ? PymcuCompiler.BuildProfiled(program.Name)
                : PymcuCompiler.BuildFixtureProfiled(program.Name);
        }
        catch (Exception ex)
        {
            // A baseline that crashes or a scenario the profiler cannot run leaves no
            // profile to build against; there is nothing to compare, so say so rather
            // than fail an axis that was never exercised.
            Assert.Inconclusive($"{program}: profile collection failed; {ex.Message}");
            return;
        }

        var plain    = BehaviorRecorder.Record(program.Optimized(), TraceBudget.Default);
        var withPgo  = BehaviorRecorder.Record(profiled,            TraceBudget.Default);

        var difference = TraceComparison.FirstDifference(plain, withPgo, TraceLabels.Pgo);
        if (difference != null)
            Assert.Fail(
                $"{program}: the profile changed what the program does.\n" +
                $"{difference}\n" +
                $"  ({TraceComparison.Summarize(plain, withPgo, TraceLabels.Pgo)})\n" +
                $"  reproduce: pymcu profile --pgo && pymcu build --profile dist/profile.json");

        if (plain.IsSilent && withPgo.IsSilent)
            Assert.Inconclusive(
                $"{program}: no observable behaviour within {TraceBudget.Default.MaxMs} ms of " +
                "simulated time; nothing was compared. The program most likely waits on a " +
                "peripheral or on UART input that this harness does not provide.");
    }

    /// <summary>
    /// Guards the axis itself. Every assertion above is vacuous if <c>--profile</c> never
    /// reaches the optimizer; the two builds would be the same image and agree trivially.
    /// The pgo-cold-outline fixture has a region the static cost model refuses to outline
    /// and the workload keeps cold, so the profiled build must differ.
    /// </summary>
    [Test]
    public void ProfileSwitch_ActuallyChangesTheEmittedImage()
    {
        if (!File.Exists(PymcuCompiler.ProfilerBinary))
            Assert.Ignore("pymcuc-avr-profiler not built (build/bin-profiler/).");

        var program = new DiffProgram(ProgramKind.Fixture, "pgo-cold-outline");

        Assert.That(PymcuCompiler.BuildFixtureProfiled(program.Name),
            Is.Not.EqualTo(program.Optimized()),
            $"{program} compiled to an identical image with and without the profile. " +
            "--profile is not reaching the optimizer (stale pymcuc binary, or the profile " +
            "did not match the program's labels), so the PGO axis is comparing a build " +
            "against itself.");
    }
}
