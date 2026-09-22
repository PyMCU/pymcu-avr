using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Expected-build-failure probes (tests/integration/fixtures/probes-life-lists/):
/// the compat-cp-life-idiomatic program rewritten the way a CircuitPython user
/// first writes the grid -- a list of rows indexed `cells[y][x]` -- in four
/// spellings, each pinned against the exact diagnostic both front ends emit.
///
/// Every variant is refused, and every refusal names the construct and says
/// why (the house rule for an unsupported-feature diagnostic):
///
///   (a)  `[[0] * width for _ in range(height)]` -- list of int lists, runtime
///        dimensions: a list comprehension only fills a fixed array of
///        compile-time-constant length; a field binding has no array to fill.
///   (b1) `[[0] * 32 for _ in range(8)]` -- literal sizes, both dimensions
///        fold: SAME refusal. The check is positional (no fixed-array binding
///        site), not a missing-constant failure, so literal sizes do not
///        rescue it.
///   (b2) `[bytearray(width) for _ in range(height)]` -- rows already fixed
///        byte arrays; only the outer container is a list: SAME refusal, the
///        outer comprehension is the construct under test.
///   (b3) `tuple(bytearray(width) for _ in range(height))` -- tuple() over a
///        genexp is a different refusal: PyMCU does not provide tuple() as a
///        runtime builtin because building a tuple needs a heap.
///
/// The diagnostics fire during IR generation, before any codegen: the message,
/// file, line and column are identical on the C# and Python front ends, which
/// the last test pins.
/// </summary>
[TestFixture]
public class ProbesLifeListsTests
{
    private const string ListCompDiagnostic =
        "error: CompileError: a list comprehension is only supported where it " +
        "fills a fixed array whose length is a compile-time constant " +
        "(`xs: uint8[4] = [f(i) for i in range(4)]`). In this position there " +
        "is no array for it to fill.";

    private const string TupleDiagnostic =
        "error: CompileError: tuple() is a Python builtin that PyMCU does not " +
        "provide: building a tuple at run time needs a heap. A tuple literal " +
        "works where the compiler can see all of its elements.";

    private static void AssertRefused(string fixture, string location, string diagnostic)
    {
        var build = () => PymcuCompiler.BuildFixture(fixture);
        build.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain(location).And.Contain(diagnostic);
    }

    [Test]
    public void A_RuntimeSizes_ListOfLists_IsRefused() =>
        AssertRefused("probes-life-lists/a-listgrid",
            "src/main.py:26:22", ListCompDiagnostic);

    [Test]
    public void B1_LiteralSizes_ListOfLists_IsRefused() =>
        AssertRefused("probes-life-lists/b1-literal-sizes",
            "src/main.py:25:22", ListCompDiagnostic);

    [Test]
    public void B2_ListOfBytearrayRows_IsRefused() =>
        AssertRefused("probes-life-lists/b2-bytearray-rows",
            "src/main.py:25:22", ListCompDiagnostic);

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
        var build = () => PymcuCompiler.BuildFixturePyParser("probes-life-lists/a-listgrid");
        build.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("src/main.py:26:22")
            .And.Contain(ListCompDiagnostic);

        var tupleBuild = () => PymcuCompiler.BuildFixturePyParser("probes-life-lists/b3-tuple-rows");
        tupleBuild.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("src/main.py:25:22")
            .And.Contain(TupleDiagnostic);
    }
}
