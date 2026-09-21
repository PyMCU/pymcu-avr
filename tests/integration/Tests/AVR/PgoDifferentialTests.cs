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
/// The profile is allowed to change *code*: it keeps hot inline-expansion regions
/// inline where the static model would have outlined them, but never *behaviour*.
/// A divergence here is an optimizer miscompile by construction, the same contract
/// as <see cref="OptimizerDifferentialTests"/>.
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
    /// The pgo-hot-veto fixture has @inline sites inside the hot loop that the static
    /// cost model outlines and the profile vetoes, so the profiled build must differ.
    /// </summary>
    [Test]
    public void ProfileSwitch_ActuallyChangesTheEmittedImage()
    {
        if (!File.Exists(PymcuCompiler.ProfilerBinary))
            Assert.Ignore("pymcuc-avr-profiler not built (build/bin-profiler/).");

        var program = new DiffProgram(ProgramKind.Fixture, "pgo-hot-veto");

        Assert.That(PymcuCompiler.BuildFixtureProfiled(program.Name),
            Is.Not.EqualTo(program.Optimized()),
            $"{program} compiled to an identical image with and without the profile. " +
            "--profile is not reaching the optimizer (stale pymcuc binary, or the profile " +
            "did not match the program's labels), so the PGO axis is comparing a build " +
            "against itself.");
    }

    /// <summary>
    /// Guards the backend leg of the axis. pgo-register-priority has no repeated
    /// @inline site the optimizer could veto, so its MIR must come out
    /// identical between the two builds -- while its loop block is hot enough
    /// under the workload to flip the R2-R15 home order. A differing hex on an
    /// identical MIR can only be pymcuc-avr consuming the profile.
    /// </summary>
    [Test]
    public void ProfileSwitch_ReachesTheBackend()
    {
        if (!File.Exists(PymcuCompiler.ProfilerBinary))
            Assert.Ignore("pymcuc-avr-profiler not built (build/bin-profiler/).");

        const string name = "pgo-register-priority";
        var profiled = PymcuCompiler.BuildFixtureProfiled(name);
        var plain    = PymcuCompiler.BuildFixture(name);

        var mirPgo   = Path.Combine(PymcuCompiler.ProfiledFixtureDir(name), "dist", "firmware.mir");
        var mirPlain = Path.Combine(PymcuCompiler.FixtureDir(name), "dist", "firmware.mir");
        Assert.That(File.ReadAllText(mirPgo), Is.EqualTo(File.ReadAllText(mirPlain)),
            $"{name}: the profiled MIR differs from the plain one -- the optimizer " +
            "acted on a fixture built to keep it out, so the hex difference below " +
            "can no longer be attributed to the backend alone.");
        Assert.That(profiled, Is.Not.EqualTo(plain),
            $"{name} compiled to an identical image with and without the profile " +
            "on an identical MIR. --profile is not reaching pymcuc-avr (stale " +
            "backend binary, or the driver stopped forwarding it).");
    }
}
