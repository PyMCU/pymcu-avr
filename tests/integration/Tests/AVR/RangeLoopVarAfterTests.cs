using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The range() loop variable keeps Python's value after the loop (PyMCU#285).
///
/// After a constant range short enough to unroll the variable read 0 (the unroller dropped
/// its constant binding without storing the last value); after a runtime range loop it held
/// `stop`, the first value not visited. Loops leaving through `break` were already right,
/// and stay so: E breaks on a limit seeded through GPIOR0, so it cannot fold.
/// </summary>
[TestFixture]
public class RangeLoopVarAfterTests
{
    private const int Gpior0Addr = 0x3E;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("range-loop-var-after"));

    private string Transcript()
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = 4;
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 500);
        return uno.Serial.Text;
    }

    [Test]
    public void AfterAnUnrolledRange_TheVariableHoldsTheLastValue()
        => Transcript().Should().Contain("A 3 2\n", "range(3) sums to 3 and leaves i == 2");

    [Test]
    public void AfterARuntimeRange_TheVariableHoldsTheLastValue()
        => Transcript().Should().Contain("B 19\n", "range(20) leaves k == 19, not 20");

    [Test]
    public void AfterADescendingRange_TheVariableHoldsTheLastValue()
        => Transcript().Should().Contain("C 1\n", "range(20, 0, -1) leaves d == 1, not 0");

    [Test]
    public void AfterABreak_TheVariableHoldsTheBreakValue()
        => Transcript().Should().Contain("D 4\nE 4\n", "a break at 4 leaves 4, whether the limit is a constant or seeded");

    [Test]
    public void AfterAContinue_TheVariableHoldsTheLastValue()
        => Transcript().Should().Contain("F 4\n", "continue does not change where range(5) ends");
}
