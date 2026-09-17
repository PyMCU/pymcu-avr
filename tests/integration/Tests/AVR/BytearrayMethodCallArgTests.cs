using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/bytearray-method-call-arg.
///
/// PyMCU#459. `d.write(bytearray([...]))` refused with "bytearray() is a Python
/// builtin that PyMCU does not provide" even though `f(bytearray([...]))` on a
/// plain function was fixed under #380: the outlined-method argument loops
/// evaluate each arg with VisitExpression directly, and VisitCall has no lowering
/// for the bytearray() builtin. Every CircuitPython busio.SPI.write()/I2C example
/// writes its buffer exactly this way.
///
/// Every expectation is CPython's answer for the same calls.
/// </summary>
[TestFixture]
public class BytearrayMethodCallArgTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("bytearray-method-call-arg"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 1000);
        return uno;
    }

    [Test]
    public void TheInlineLiteral_ReachesTheMethod()
    {
        Boot().Serial.Text.Should().Contain("10\n20\n",
            "bytearray([10, 20]) written inline must reach the method as real storage, "
            + "not be refused");
    }

    [Test]
    public void TheInlineCountForm_ReachesTheMethodZeroFilled()
    {
        Boot().Serial.Text.Should().Contain("20\n0\n0\n",
            "bytearray(2) written inline must reach the method zero-filled");
    }

    [Test]
    public void TheKeywordForm_BindsTheSameWay()
    {
        Boot().Serial.Text.Should().Contain("0\n3\n4\n",
            "d.write(buf=bytearray([3, 4])) binds the buffer by keyword");
    }
}

/// <summary>
/// The same fixture under the Python-frontend parser (PyMCU#459 applies to both
/// frontends -- the refusal lived in shared IR generation).
/// </summary>
[TestFixture]
public class BytearrayMethodCallArgPyParserTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixturePyParser("bytearray-method-call-arg"));

    [Test]
    public void BothForms_ReachTheMethod()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 1000);
        uno.Serial.Text.Should().Contain("10\n20\n0\n0\n3\n4\n");
    }
}
