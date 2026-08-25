using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/method-instance-param (PyMCU#123).
///
/// A method taking another instance was outlined, and a shared body cannot receive an
/// instance: `self` arrived and the parameter did not, so the answer was the sum with one
/// operand missing. The two instances hold different values, because with equal values an
/// answer that dropped one of them could still look right.
/// </summary>
[TestFixture]
public class MethodInstanceParamTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("method-instance-param"));

    [Test]
    public void BothOperandsReachTheMethod()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Contain("8\n", "7 + 1, not 7 with the parameter missing");
    }
}
