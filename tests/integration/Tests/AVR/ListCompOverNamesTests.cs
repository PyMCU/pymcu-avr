using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/listcomp-over-names (PyMCU#84).
///
/// A comprehension over a name, and one with two `for` clauses, were both rejected for having
/// a filter neither of them has. The indices are read at run time so the values have to be in
/// the array rather than folded into the print.
/// </summary>
[TestFixture]
public class ListCompOverNamesTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("listcomp-over-names"));

    [Test]
    public void AComprehensionOverANameHoldsItsValues()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().StartWith("2\n", "base[0] is 1 and the comprehension doubles it");
    }

    [Test]
    public void TwoForClausesProduceTheCrossProduct()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Contain("2\n1\ndone\n", "grid[4] is 1 * 1, the middle of [0,0,0, 0,1,2, 0,2,4]");
    }
}
