using FluentAssertions;
using NUnit.Framework;
using PyMCU.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The compat-cp-life-idiomatic program rewritten the way a CircuitPython user
/// first writes the grid -- a list of rows indexed `cells[y][x]` -- in four
/// spellings (tests/integration/fixtures/probes-life-lists/).
///
/// Three of the four spellings are now a compile-time 2-D grid lowered to ONE
/// flat fixed array, so they build and run the same Life the flat fixture
/// does -- the wire stream is compared against the compat-cp-life-idiomatic
/// oracle (the same 30-generation program: 253 transactions):
///
///   (a)  `[[0] * width for _ in range(height)]` -- list of int lists; the
///        constructor arguments are literals at the only call site, so both
///        dimensions fold.
///   (b1) `[[0] * 32 for _ in range(8)]` -- literal sizes.
///   (b2) `[bytearray(width) for _ in range(height)]` -- bytearray rows.
///   (b3) `tuple(bytearray(width) for _ in range(height))` -- tuple() over a
///        genexp stays refused: building a tuple needs a heap.
/// </summary>
[TestFixture]
public class ProbesLifeListsTests
{
    private const byte OledAddr = 0x3C;
    private const int LifeTransactions = 253;

    private const string TupleDiagnostic =
        "error: CompileError: tuple() is a Python builtin that PyMCU does not " +
        "provide: building a tuple at run time needs a heap. A tuple literal " +
        "works where the compiler can see all of its elements.";

    // The probes run the same 30-generation Life as compat-cp-life-idiomatic,
    // so its CPython oracle is the expected stream for every accepted probe.
    private static List<I2cTransaction> LifeOracle() => OracleScript.Run(
        Path.Combine(PymcuCompiler.FixtureDir("compat-cp-life-idiomatic"), "oracle", "oracle.py"),
        Path.Combine(PymcuCompiler.Root, ".venv", "bin", "python"));

    private static void AssertRefused(string fixture, string location, string diagnostic)
    {
        var build = () => PymcuCompiler.BuildFixture(fixture);
        build.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain(location).And.Contain(diagnostic);
    }

    // (a)/(b1)/(b2): each spelling lowers to one flat uint8[256] and runs the
    // same Life -- same firmware on both front ends, same wire traffic as the
    // hand-flattened fixture's oracle.
    private static void AssertCompilesAndRunsLife(string fixture)
    {
        var hex = PymcuCompiler.BuildFixture(fixture);
        var pyHex = PymcuCompiler.BuildFixturePyParser(fixture);
        pyHex.Should().Be(hex,
            "the grid spelling must compile identically on both front ends");

        // One flat array per grid: the two cell buffers sit 256 = 32*8 bytes
        // apart in the listing, with no per-row storage symbols.
        var asm = File.ReadAllText(
            Path.Combine(PymcuCompiler.FixtureDir(fixture), "dist", "firmware.gas.asm"));
        foreach (var name in new[] { "life_cells", "life_next_cells" })
        {
            System.Text.RegularExpressions.Regex.Matches(asm, $@"\.equ\s+{name},")
                .Count.Should().Be(1, $"{name} must lower to ONE flat array");
        }
        var cellsOff = System.Text.RegularExpressions.Regex
            .Match(asm, @"\.equ\s+life_cells,\s*_stack_base \+ (\d+)").Groups[1].Value;
        var nextOff = System.Text.RegularExpressions.Regex
            .Match(asm, @"\.equ\s+life_next_cells,\s*_stack_base \+ (\d+)").Groups[1].Value;
        (int.Parse(nextOff) - int.Parse(cellsOff)).Should().Be(256,
            "life_cells must be a flat 32*8 array, not a list of row objects");

        var run = UnoTwiTrace.Record(hex, OledAddr, LifeTransactions, maxMs: 20_000);
        I2cStreams.AssertEqual(LifeOracle(), run, fixture);
    }

    [Test]
    public void A_RuntimeSizes_ListOfLists_CompilesAndRuns() =>
        AssertCompilesAndRunsLife("probes-life-lists/a-listgrid");

    [Test]
    public void B1_LiteralSizes_ListOfLists_CompilesAndRuns() =>
        AssertCompilesAndRunsLife("probes-life-lists/b1-literal-sizes");

    [Test]
    public void B2_ListOfBytearrayRows_CompilesAndRuns() =>
        AssertCompilesAndRunsLife("probes-life-lists/b2-bytearray-rows");

    [Test]
    public void B3_TupleOfBytearrayRows_IsRefused() =>
        AssertRefused("probes-life-lists/b3-tuple-rows",
            "src/main.py:25:22", TupleDiagnostic);

    [Test]
    public void BothFrontEnds_RefuseIdentically()
    {
        // Same span, same message on the Python-parser front end -- a diagnostic
        // that moved or reworded between parsers would mean two surfaces to
        // keep honest.
        var tupleBuild = () => PymcuCompiler.BuildFixturePyParser("probes-life-lists/b3-tuple-rows");
        tupleBuild.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("src/main.py:25:22")
            .And.Contain(TupleDiagnostic);
    }
}
