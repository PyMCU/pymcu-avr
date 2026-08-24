using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/literal-width.
///
/// An unannotated integer literal used to infer int32 whatever its value, which cost 756
/// bytes more than the annotated spelling of the same program (PyMCU#62). The width is now
/// the narrowest type that holds every literal assigned to the name.
///
/// Narrowing is a correctness question before it is a size question, so these assert the
/// values that come out: a name assigned 5 and then 300 must still print 300, a negative
/// must stay signed, and a name whose assignments are not all literals must be untouched.
/// </summary>
[TestFixture]
public class LiteralWidthTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("literal-width"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 500);
        return uno;
    }

    [Test]
    public void SingleLiteral_KeepsItsValue()
    {
        Boot().Serial.Text.Should().Contain("200\n", "a = 200 narrows to uint8 and is still 200");
    }

    [Test]
    public void TwoLiterals_TakeTheWidthThatHoldsBoth()
    {
        Boot().Serial.Text.Should().Contain("300\n", "b = 5 then b = 300 must print 300, not 44");
    }

    [Test]
    public void NegativeLiteral_StaysSigned()
    {
        Boot().Serial.Text.Should().Contain("-5\n", "c = -5 narrows to a signed type");
    }

    [Test]
    public void LiteralWiderThan16Bits_IsNotTruncated()
    {
        Boot().Serial.Text.Should().Contain("70000\n", "d = 70000 needs 32 bits and must keep them");
    }

    [Test]
    public void NameWithANonLiteralAssignment_IsLeftAlone()
    {
        Boot().Serial.Text.Should().Contain("300\n", "e = 200 then e = e + 100 is 300 either way");
    }
}
