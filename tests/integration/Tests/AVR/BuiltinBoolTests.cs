using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/builtin-bool.
///
/// bool(x) used to be reported as an undefined function with the suggestion that it might be a
/// typo or a missing import, neither of which can apply to a builtin. It is now Python's truth
/// test, which for every value this target can hold is "not zero", producing the same 0/1 a
/// materialized comparison does.
///
/// Data-space addresses used: GPIOR0 = 0x3E, GPIOR1 = 0x4A, GPIOR2 = 0x4B
///
/// Checkpoints:
///   1 — bool(0) = 0, bool(200) = 1
///   2 — bool(256) = 1 (the test is on the value, not on its low byte)
///   3 — bool(a) + bool(b) counts the non-zero operands
///   4 — bool(-7) = 1
/// </summary>
[TestFixture]
public class BuiltinBoolTests
{
    private SimSession _session = null!;

    // ATmega328P data-space addresses
    private const int GPIOR1_ADDR = 0x4A;
    private const int GPIOR2_ADDR = 0x4B;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("builtin-bool"));

    private static void SkipBreaks(ArduinoUnoSimulation uno, int count)
    {
        for (var i = 0; i < count; i++)
        {
            uno.RunToBreak();
            uno.RunInstructions(1); // step over the BREAK opcode
        }
    }

    private ArduinoUnoSimulation AtCheckpoint(int n)
    {
        var uno = _session.Reset();
        SkipBreaks(uno, n - 1);
        uno.RunToBreak();
        return uno;
    }

    [Test]
    public void Bool_ZeroIsFalse_NonZeroIsTrue()
    {
        var uno = AtCheckpoint(1);
        uno.Data[GPIOR1_ADDR].Should().Be(0, "bool(0) is False, which stores as 0");
        uno.Data[GPIOR2_ADDR].Should().Be(1, "bool(200) is True, which stores as 1");
    }

    [Test]
    public void Bool_TestsTheWholeValue_NotItsLowByte()
    {
        var uno = AtCheckpoint(2);
        uno.Data[GPIOR1_ADDR].Should().Be(1,
            "256 is non-zero even though its low byte is 0, so bool(256) is True");
    }

    [Test]
    public void Bool_ResultIsAZeroOrOne_UsableInArithmetic()
    {
        var uno = AtCheckpoint(3);
        uno.Data[GPIOR1_ADDR].Should().Be(1, "one of the two operands is non-zero");
        uno.Data[GPIOR2_ADDR].Should().Be(2, "bool() yields exactly 1, so 1 + 1 = 2");
    }

    [Test]
    public void Bool_NegativeIsTrue()
    {
        var uno = AtCheckpoint(4);
        uno.Data[GPIOR1_ADDR].Should().Be(1, "-7 is non-zero, so bool(-7) is True");
    }
}
