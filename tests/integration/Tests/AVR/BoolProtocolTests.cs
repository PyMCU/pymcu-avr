using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/bool-protocol (PyMCU#121).
///
/// __bool__ was consulted only when the instance was the whole condition of an if or a while.
/// Every case here answers TRUE on purpose: the bug produced false, so a fixture built around
/// a false answer would have passed while the bug was live, which is how an earlier sweep
/// came to record __bool__ as working.
/// </summary>
[TestFixture]
public class BoolProtocolTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("bool-protocol"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        return uno.Serial.Text;
    }

    [Test]
    public void TheStatementFormWasNeverBroken()
    {
        Boot().Should().StartWith("if yes\n");
    }

    [Test]
    public void AConditionalExpressionAsksTheObject()
    {
        Boot().Should().Contain("if yes\n1\n", "1 if x else 0, with __bool__ true");
    }

    [Test]
    public void NotAsksTheObject()
    {
        Boot().Should().Contain("not: yes\n");
    }

    [Test]
    public void AndOrOperandsAskTheObject()
    {
        Boot().Should().Contain("and: yes\nor: yes\ndone\n");
    }
}
