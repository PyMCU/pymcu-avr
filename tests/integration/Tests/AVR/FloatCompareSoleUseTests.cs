using System;
using System.Linq;
using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/float-compare-sole-use (PyMCU#388): a float whose only use is a COMPARISON is
/// still a float.
///
/// The set that decides which register layout a float parameter is spilled with at function
/// entry was built by walking only the instructions that PRODUCE a value -- a binary op, a
/// copy, a return, a call. A parameter whose every use is a comparison was never seen as a
/// float, so it was spilled with the R24-anchored uint32 layout and read back by the float
/// path with the R22-anchored C layout. The two 16-bit halves swap, and the comparison decides
/// on a number nobody wrote: every `>` answered false and every `<` answered true, for every
/// pair of values.
///
/// fixtures/float-compare did not catch it because its operands are subtractions: one float
/// arithmetic op ahead of the comparison rewrites the slot in the layout the comparison reads.
/// That is also why `self._t + 0.0 > 0.05` was right where `self._t > 0.05` was wrong.
/// </summary>
[TestFixture]
public class FloatCompareSoleUseTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("float-compare-sole-use"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("float-compare-sole-use"));
    }

    private static string[] Lines(SimSession s)
    {
        var uno = s.Reset();
        uno.RunMilliseconds(400);
        return uno.Serial.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                              .Select(l => l.Trim()).ToArray();
    }

    // Measured by running the same file under CPython, not read off the firmware.
    private static readonly string[] Cpython =
    {
        "field-gt 1", "field-ge 1", "field-lt 1", "field-via-local 1", "field-plus-zero 1",
        "param-gt-true 1", "param-gt-false 0", "param-gt-big 1", "param-lt-false 0",
        "param-lt-big 0", "runtime-gt 1", "runtime-gt-reversed 0", "END",
    };

    [Test]
    public void TheValuesDecideTheComparison()
        => Lines(_session).Should().Equal(Cpython,
            "both directions of each relation are here, so a comparison that ignores its "
            + "operands cannot pass");

    [Test]
    public void TheSameThroughThePythonFrontEnd()
        => Lines(_pySession).Should().Equal(Cpython);

    [Test]
    public void AFloatParameterReadOnlyByAComparisonArrivesIntact()
        => Lines(_session).Should().ContainInOrder(
            new[] { "param-gt-true 1", "param-gt-false 0" },
            "0.1 > 0.05 and 0.1 > 0.2 cannot both be decided by the layout");

    [Test]
    public void AFloatFieldComparedDirectlyAgreesWithTheSameFieldPlusZero()
        => Lines(_session).Should().ContainInOrder(
            new[] { "field-gt 1", "field-plus-zero 1" },
            "the arithmetic ahead of the comparison was the only thing making it right");
}
