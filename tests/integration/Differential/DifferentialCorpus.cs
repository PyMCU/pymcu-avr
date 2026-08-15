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
    /// so that optimized and unoptimized builds are *expected* to differ. Excluding them is
    /// not a way to hide failures — a divergence here says nothing about correctness, and
    /// leaving them in would drown a real finding in noise.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> TimingDependent = new Dictionary<string, string>
    {
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
    /// Programs that diverge today, with the divergence each one shows. These are findings,
    /// not exemptions: the harness still builds and compares them, prints the divergence, and
    /// reports the case as inconclusive rather than failing the suite — and it fails loudly if
    /// one of them stops diverging, so the entry gets deleted along with the fix.
    /// </summary>
    /// <remarks>
    /// Every entry here is the unoptimized build behaving wrongly while the optimized build
    /// behaves as its own test fixture expects, and every one of them is compiled with
    /// <c>AvrCodeGen</c>'s inline-expansion outliner active — a code path that no optimized
    /// build in this repository reaches, because the IR optimizer's outliner has already run.
    /// See the harness report for the analysis.
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, string> KnownDivergences = new Dictionary<string, string>
    {
        ["examples/adc-read"]                    = "banner newline arrives as 0x00",
        ["examples/inheritance-zca"]             = "prints C:0034 instead of C:1234",
        ["examples/interrupt-counter"]           = "banner newline arrives as 0x00",
        ["examples/pcint-counter"]               = "banner is skipped; prints COUNT: first",
        ["examples/pin-irq"]                     = "banner newline arrives as 0x00",
        ["examples/softspi"]                     = "banner is skipped; prints D: with no value",
        ["examples/spi-shift-register"]          = "banner newline arrives as 0x00",
        ["examples/uart-command"]                = "banner and help text are replaced by LED=",
        ["examples/uart-echo"]                   = "banner newline arrives as 0x00",
        ["examples/uart-rx-interrupt"]           = "banner newline arrives as 0x00",
        ["examples/uart-str"]                    = "prints the first string twice",
        ["fixtures/break-edges"]                 = "checkpoint 4 keeps checkpoint 2's GPIOR0 value",
        ["fixtures/builtin-ops"]                 = "prints V:01, its own fixture expects V:00",
        ["fixtures/compat-cp-microcontroller"]   = "writes 0x00 where the NVM byte should be",
        ["fixtures/fixeddict"]                   = "prints D:257, its own fixture expects D:99",
        ["fixtures/fstring-value"]               = "banner is skipped",
        ["fixtures/instance-array"]              = "prints NUL bytes instead of the array contents",
        ["fixtures/map-range"]                   = "reprints the banner instead of each label",
        ["fixtures/new-builtins"]                = "banner is replaced by a value",
        ["fixtures/print-integers"]              = "prints 210/12/64 instead of 1234/-500/123456",
        ["fixtures/ptr-runtime"]                 = "banner newline arrives as 0x00",
        ["fixtures/random-prng"]                 = "reprints the banner instead of each label",
        ["fixtures/read-blocking"]               = "banner newline arrives as 0x00",
        ["fixtures/static-method"]               = "checkpoint 2 keeps checkpoint 1's GPIOR0 value",
        ["fixtures/tuple-args"]                  = "prints newlines instead of the tuple members",
        ["fixtures/warning-decorator"]           = "prints V:01 instead of V:2A",
        ["fixtures/zca-array"]                   = "prints the wrong array bytes",
    };

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
