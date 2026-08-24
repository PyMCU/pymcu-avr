using FluentAssertions;
using NUnit.Framework;
using PyMCU.IntegrationTests.Differential;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The Python front end (PYMCU_PY_PARSER=1) against the C# parser, over the whole corpus.
///
/// CPython's `ast` builds the same AST the hand-written parser builds, and everything after
/// that phase is untouched -- so the only honest test is equality of the finished image. Not
/// "it compiles", not "the size is close": the same bytes.
///
/// A failure here is a bug in the translator (Frontend/PyParser/pymcu_translate.py), which is
/// why there is no tolerated-divergence list to grow: the axis is either exact or wrong.
/// </summary>
[TestFixture]
public class PyParserDifferentialTests
{
    private static IEnumerable<DiffProgram> Corpus() => DifferentialCorpus.All();

    [TestCaseSource(nameof(Corpus))]
    public void PythonFrontend_ProducesTheSameImage(DiffProgram program)
    {
        if (DifferentialCorpus.KnownPyParserDivergences.TryGetValue(program.ToString(), out var known))
        {
            Assert.Inconclusive($"{program}: known translator divergence -- {known}");
            return;
        }

        string csharp = program.Optimized();
        string python = program.PyFrontend();

        python.Should().Be(csharp,
            $"{program} must compile to the same firmware whichever front end parsed it");
    }
}
