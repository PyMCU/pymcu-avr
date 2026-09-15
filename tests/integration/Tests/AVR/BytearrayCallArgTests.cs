using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/bytearray-call-arg.
///
/// PyMCU#380. `f(bytearray([...]))` refused with the same "unsupported Python builtin"
/// message a field target did (#392): the argument-evaluation loop for a plain (outlined)
/// function call falls to VisitExpression on anything that is not a known-array
/// VariableExpr, and VisitCall has no lowering for the bytearray() builtin. Every
/// CircuitPython busio.SPI.write()/I2C example writes its buffer exactly this way.
///
/// Every expectation is CPython's answer for the same list literal.
/// </summary>
[TestFixture]
public class BytearrayCallArgTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("bytearray-call-arg"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 1000);
        return uno;
    }

    [Test]
    public void TheFirstElement_OfTheInlineLiteral_IsLaidOutCorrectly()
    {
        Boot().Serial.Text.Should().Contain("10\n",
            "bytearray([10, 20, 30])[0] must reach the callee as real storage, not be refused");
    }

    [Test]
    public void TheSecondElement_IsLaidOutCorrectly()
    {
        Boot().Serial.Text.Should().Contain("20\n");
    }

    [Test]
    public void TheThirdElement_IsLaidOutCorrectly()
    {
        Boot().Serial.Text.Should().Contain("30\n");
    }
}
