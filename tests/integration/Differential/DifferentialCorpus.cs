using System.Text.RegularExpressions;

namespace PyMCU.IntegrationTests.Differential;

/// <summary>Where a corpus program lives, which decides how it is built.</summary>
public enum ProgramKind { Example, Fixture }

/// <summary>One program the differential harness runs, built both ways.</summary>
public sealed record DiffProgram(ProgramKind Kind, string Name)
{
    public string Optimized() => Kind == ProgramKind.Example
        ? PymcuCompiler.Build(Name)
        : PymcuCompiler.BuildFixture(Name);

    public string Unoptimized() => Kind == ProgramKind.Example
        ? PymcuCompiler.BuildUnoptimized(Name)
        : PymcuCompiler.BuildFixtureUnoptimized(Name);

    /// <summary>Same IR as <see cref="Optimized"/>, with the AVR backend peephole switched off.</summary>
    public string NoPeephole() => Kind == ProgramKind.Example
        ? PymcuCompiler.BuildNoPeephole(Name)
        : PymcuCompiler.BuildFixtureNoPeephole(Name);

    /// <summary>The same program parsed by CPython instead of the C# parser.</summary>
    public string PyFrontend() => Kind == ProgramKind.Example
        ? PymcuCompiler.BuildPyParser(Name)
        : PymcuCompiler.BuildFixturePyParser(Name);

    public override string ToString() => Kind == ProgramKind.Example ? $"examples/{Name}" : $"fixtures/{Name}";
}

/// <summary>
/// The set of already-existing PyMCU programs the differential harness compiles twice.
/// Nothing here is written for the harness: it is the showcase examples under
/// <c>examples/</c> and the compiler fixtures under <c>tests/integration/fixtures/</c>,
/// which between them cover most of the language and the whole AVR HAL.
/// </summary>
public static class DifferentialCorpus
{
    private static readonly Regex TargetKey = new(@"^\s*target\s*=\s*[""']([^""']+)[""']", RegexOptions.Multiline);
    private static readonly Regex BoardKey  = new(@"^\s*board\s*=\s*[""']([^""']+)[""']",  RegexOptions.Multiline);

    /// <summary>
    /// Programs whose observable behaviour legitimately depends on how fast the code runs,
    /// so that two builds of different speed are *expected* to differ. Excluding them is
    /// not a way to hide failures — a divergence here says nothing about correctness, and
    /// leaving them in would drown a real finding in noise. Applies to every axis: the
    /// peephole changes instruction counts just as the IR optimizer does.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> TimingDependent = new Dictionary<string, string>
    {
        ["fixtures/pwm-dual-channel"] =
            "six free-running hardware PWM channels at two periods; which output edge lands " +
            "first after init depends on exact init-code timing, so the GPIO change order " +
            "differs between builds while every register value is identical",
        ["examples/rtos-multitask"] =
            "preemptive scheduler — how many pin writes each task gets in between timer ticks " +
            "is a function of how fast the code is",
        ["examples/dht-sensor"] =
            "bit-banged 1-wire read with no sensor attached; which microsecond the read times " +
            "out in decides whether it prints a reading or an error",
        ["fixtures/dht-sensor-mp"] = "same bit-banged 1-wire timeout as examples/dht-sensor",
        ["fixtures/dht-sensor-dht22"] = "same bit-banged 1-wire timeout as examples/dht-sensor",
        ["fixtures/async-timebase"] =
            "asyncio polls a millisecond time base; the number of not-ready polls before a task " +
            "becomes runnable falls straight out of the execution speed",
    };

    /// <summary>
    /// IR-optimizer axis. Programs that diverge today, with the divergence each one shows. These are findings,
    /// not exemptions: the harness still builds and compares them, prints the divergence, and
    /// reports the case as inconclusive rather than failing the suite — and it fails loudly if
    /// one of them stops diverging, so the entry gets deleted along with the fix.
    /// </summary>
    /// <remarks>
    /// Every entry here is the unoptimized build behaving wrongly while the optimized build
    /// behaves as its own test fixture expects. The first two are compiled with
    /// <c>AvrCodeGen</c>'s inline-expansion outliner active — a code path that no optimized
    /// build in this repository reaches, because the IR optimizer's outliner has already run.
    /// See the harness report for the analysis. The third has a different root, recorded with
    /// its entry.
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, string> KnownDivergences = new Dictionary<string, string>
    {
        ["fixtures/print-integers"]              = "prints 210/12/64 instead of 1234/-500/123456",
        ["fixtures/literal-width-module"]        = "prints 44/251/112 instead of 300/-5/70000: the " +
            "unoptimized build clears the high byte (CLR R25) before passing a widened global to " +
            "the outlined writer",
    };

    /// <summary>
    /// Peephole axis. Same contract as <see cref="KnownDivergences"/>, for the programs whose
    /// behaviour changes when <c>PYMCU_NO_PEEPHOLE=1</c> switches the AVR backend peephole off
    /// with the IR optimizer left on in both builds. Empty is the expected state: an entry here
    /// is an open miscompile in <c>AvrPeephole</c>, since neither build's IR differs.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> KnownPeepholeDivergences =
        new Dictionary<string, string>();

    /// <summary>
    /// Python-front-end axis. The AST is the contract, so the image must be identical, byte
    /// for byte -- empty is the only acceptable state here. An entry means the translator
    /// builds a different tree than the C# parser for that program, which is a bug in the
    /// translator, not a tolerated difference.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> KnownPyParserDivergences =
        new Dictionary<string, string>();

    /// <summary>Every atmega328p program in the repository, minus the timing-dependent ones.</summary>
    public static IEnumerable<DiffProgram> All()
    {
        foreach (var program in Enumerate())
            if (!TimingDependent.ContainsKey(program.ToString()))
                yield return program;
    }

    /// <summary>Every atmega328p program, including the ones excluded from the suite.</summary>
    public static IEnumerable<DiffProgram> Enumerate()
    {
        foreach (var dir in Directories(Path.Combine(PymcuCompiler.Root, "examples")))
            if (RunsOnUno(dir)) yield return new DiffProgram(ProgramKind.Example, dir.Name);

        foreach (var dir in Directories(Path.Combine(PymcuCompiler.Root, "tests", "integration", "fixtures")))
            if (RunsOnUno(dir)) yield return new DiffProgram(ProgramKind.Fixture, dir.Name);
    }

    private static IEnumerable<DirectoryInfo> Directories(string root) =>
        new DirectoryInfo(root).EnumerateDirectories()
            .Where(d => File.Exists(Path.Combine(d.FullName, "pyproject.toml")))
            .OrderBy(d => d.Name, StringComparer.Ordinal);

    // ArduinoUnoSimulation is an ATmega328P. Projects targeting another chip (the ATtiny
    // examples) have no board to run on here and are left to their own fixtures.
    private static bool RunsOnUno(DirectoryInfo dir)
    {
        var toml = File.ReadAllText(Path.Combine(dir.FullName, "pyproject.toml"));
        var target = TargetKey.Match(toml);
        if (target.Success) return target.Groups[1].Value.Equals("atmega328p", StringComparison.OrdinalIgnoreCase);

        var board = BoardKey.Match(toml);
        return board.Success && board.Groups[1].Value is "arduino_uno" or "uno";
    }
}
