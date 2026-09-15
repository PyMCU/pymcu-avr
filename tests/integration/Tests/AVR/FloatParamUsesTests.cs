using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/float-param-uses: a float parameter exposed to one instruction kind at a time.
///
/// The backend builds the set of float-typed names to decide which register layout each one is
/// spilled with at function entry: floats use the GCC layout anchored at R22, integers the one
/// anchored at R24. That set was a hand-written list of instruction kinds, so a float whose
/// EVERY use was a kind the list did not name was spilled as an integer and read back as a
/// float. The two 16-bit halves swap and the value that comes out is not the value that went
/// in, with no diagnostic anywhere.
///
/// PyMCU#388 closed it for COMPARISONS. It stayed open for the rest: measured on atmega328p,
/// `-t` on a float parameter answered `-0.0` for every value. The set asks the backend's one
/// exhaustive Val walker now, which is where the knowledge already lived, so a kind cannot be
/// missing from it and present in the walker.
///
/// A green build proves nothing here, because it always built. Only the printed number does.
/// </summary>
[TestFixture]
public class FloatParamUsesTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("float-param-uses"));

    private static string Output()
    {
        var uno = _session.Reset();
        uno.RunMilliseconds(400);
        return uno.Serial.Text;
    }

    [Test]
    public void AFloatParameterUsedOnlyByANegationKeepsItsValue()
        => Output().Should().Contain("neg -2.25",
            "it answered -0.0 for every value: the parameter was spilled with the integer "
            + "layout and read back with the float one");

    [Test]
    public void AFloatParameterUsedOnlyByAComparisonStillAnswers()
        => Output().Should().Contain("cmp 1", "2.25 > 0.5 -- the case #388 closed on its own");

    [Test]
    public void AFloatParameterUsedByArithmeticIsUnchanged()
        => Output().Should().Contain("add 3.25", "the control: 2.25 + 1.0");

    [Test]
    public void TheProgramRunsToItsEnd() => Output().Should().Contain("done");
}
