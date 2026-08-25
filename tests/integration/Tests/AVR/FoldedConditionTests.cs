using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/folded-condition (PyMCU#137).
///
/// A folded non-comparison used bare as a condition took the else branch. The fixture checks
/// the true cases AND that the genuinely false ones stay false, because the fix narrows the
/// branch that decides constant conditions and over-correcting it would make everything true.
/// </summary>
[TestFixture]
public class FoldedConditionTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("folded-condition"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        return uno.Serial.Text;
    }

    [Test]
    public void ATruthyFoldedExpressionTakesTheThenBranch()
    {
        Boot().Should().Contain("and\nadd\nor\nshift\nmul\n");
    }

    [Test]
    public void ComparisonsAreUnaffected()
    {
        Boot().Should().Contain("lt\neq\ndone\n");
    }

    [Test]
    public void AFalsyFoldedExpressionStillTakesTheElseBranch()
    {
        Boot().Should().NotContain("SHOULD NOT PRINT");
    }
}
