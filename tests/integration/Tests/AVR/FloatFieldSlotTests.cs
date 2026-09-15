using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/float-field-slot (PyMCU#404): a float field alongside a second field.
///
/// A class with ONE field collapses to a scalar. With two or more it is boxed into a byte slot
/// in SRAM, and the constructor split each field into its bytes with an arithmetic shift --
/// `value >> 8`, `>> 16`, `>> 24`. A float's bytes are its IEEE-754 representation and not an
/// arithmetic quantity, so the AVR backend refused the shift, naming the constructor's line and
/// a pass rather than a cause. It refused an ordinary class: a driver keeping a timeout
/// alongside a pin count has exactly this shape.
///
/// A float field is reinterpreted as 32 bits before the split now, which is what the READ side
/// has always done -- a multi-byte field comes back through one typed load over the same four
/// bytes. So what is measured is the ROUND TRIP, with the field ORDER varied, because the
/// float's offset inside the slot is what changes between the two: a build that succeeds while
/// storing three of the four bytes would pass a build check and print a plausible wrong number.
///
/// Every read here goes THROUGH A METHOD, because that is the path that reaches the slot. A
/// direct `a._t` from outside the class reads a flattened variable that the slot construction
/// never writes; it answered correctly for one of the two classes here, by the accident of a
/// constant being propagated into it, and 0.0 for the other. That is a separate bug and would
/// have measured the wrong thing.
///
/// Measured: 7 of 7 red on the unmodified compiler, where the fixture does not build at all.
/// </summary>
[TestFixture]
public class FloatFieldSlotTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("float-field-slot"));

    private static string Output()
    {
        var uno = _session.Reset();
        uno.RunMilliseconds(400);
        return uno.Serial.Text;
    }

    [Test]
    public void AMethodOverAFloatFieldAnswers()
        => Output().Should().Contain("gt 1",
            "0.1 > 0.05; the program did not build at all before");

    [Test]
    public void TheFloatFieldReadsBackTheBytesItWasGiven()
        => Output().Should().Contain("t 0.1",
            "the round trip is the measurement: three of four bytes stored would print a "
            + "plausible wrong number rather than fail");

    [Test]
    public void TheSecondFieldIsUndisturbed()
        => Output().Should().Contain("pad 3");

    [Test]
    public void AFloatFieldAtANonZeroOffsetAnswersTheSame()
        => Output().Should().Contain("rev-gt 1", "0.25 > 0.2, with the float second in the slot");

    [Test]
    public void AFloatFieldAtANonZeroOffsetReadsBack()
        => Output().Should().Contain("rev-t 0.25");

    [Test]
    public void TheIntegerFieldBeforeTheFloatIsUndisturbed()
        => Output().Should().Contain("rev-n 7");

    [Test]
    public void TheProgramRunsToItsEnd() => Output().Should().Contain("done");
}
