using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/literal-width-module.
///
/// The same widths one scope up, where the failure was not cost but a wrong number: an
/// unannotated module-level integer kept the width of its FIRST assignment and truncated
/// everything after it, so `b = 5` then `b = 300` printed 44 (PyMCU#76).
///
/// Module level is the MicroPython and CircuitPython shape -- those programs have no
/// def main() at all -- so this was the default spelling for anyone arriving from a port.
/// </summary>
[TestFixture]
public class LiteralWidthModuleTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("literal-width-module"));

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
