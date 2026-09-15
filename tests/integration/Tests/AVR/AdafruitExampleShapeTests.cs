using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/adafruit-example-shape (PyMCU#374, #375): the two constructs an Adafruit example
/// opens with, measured as TEXT ON THE WIRE rather than as a build that succeeds.
///
/// `hcsr04_simpletest.py` builds its sensor at module level with a keyword-only default it does
/// not pass, and prints a ONE-ELEMENT TUPLE, because the Mu plotter reads a printed tuple. The
/// first was reported as a name the function never received; the second as a runtime value.
///
/// Neither has to exist at run time, and what this file pins is that the OUTPUT is the one
/// CPython produces for the same values. A build that succeeds and prints `(12.5)` would be a
/// different program than the example: the trailing comma is how CPython tells a one-element
/// tuple from a parenthesised number, and it is the character the plotter reads.
/// </summary>
[TestFixture]
public class AdafruitExampleShapeTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("adafruit-example-shape"));

    private static string Output()
    {
        var uno = _session.Reset();
        uno.RunMilliseconds(400);
        return uno.Serial.Text;
    }

    [Test]
    public void AOneElementTupleKeepsItsTrailingComma()
    {
        Output().Should().Contain("(12.5,)",
            "the trailing comma is how CPython tells a one-element tuple from a "
            + "parenthesised number, and it is the character the Mu plotter reads");
    }

    [Test]
    public void ATwoElementTupleIsSeparatedTheWayCPythonSeparatesIt()
    {
        Output().Should().Contain("(1, 2)", "comma AND space, as CPython writes it");
    }

    [Test]
    public void AnOmittedKeywordOnlyFloatDefaultReachesTheField()
    {
        Output().Should().Contain("0.1",
            "the constructor was called without the default, so the field holds what the "
            + "signature says it holds");
    }

    [Test]
    public void TheProgramRunsToItsEnd()
    {
        Output().Should().Contain("done");
    }
}
