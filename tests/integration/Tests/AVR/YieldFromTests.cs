using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/yield-from.
///
/// `yield from` was a parser rejection. It is expanded, not nested: the delegate's body is
/// spliced in with its locals renamed before the state split, so what runs is a single flat
/// state machine. Each test picks the shape that would break a different way -- a local name
/// shared with the delegate, a delegation that has to re-arm on the next pass of a loop, and
/// two levels of it.
/// </summary>
[TestFixture]
public class YieldFromTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("yield-from"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 2000);
        return uno.Serial.Text;
    }

    [Test]
    public void PlainDelegationYieldsTheDelegatesValues()
    {
        Boot().Should().StartWith("0\n1\n2\n-\n");
    }

    [Test]
    public void ALocalSharedWithTheDelegateIsNotClobbered()
    {
        Boot().Should().Contain("100\n0\n1\n2\n100\n-\n",
            "the delegate's own `i` counts to 3 while the caller's `i` stays 100");
    }

    [Test]
    public void DelegationInsideALoopReArms()
    {
        Boot().Should().Contain("0\n1\n0\n1\n-\n", "the second pass runs the delegate from the start again");
    }

    [Test]
    public void TwoLevelsOfDelegationReachTheLeaf()
    {
        Boot().Should().Contain("7\n8\ndone\n");
    }
}
