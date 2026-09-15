using System;
using System.Linq;
using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/none-then-value-width (PyMCU#385): a name gets its WIDTH from the value finally
/// stored in it.
///
/// Two ways a name was left a byte and truncated what was put in it. `x = None` first: None is
/// not a value and emits no store, but the empty binding it left behind was read as a
/// measurement, so a 570 us pulse came back as 58. And a FRESH local inside a body that is
/// expanded rather than compiled once: the fallback binding is a byte, so a uint16 kept its
/// low byte and a float read back short.
///
/// Both are what the acceptance for #385 hit once the wait loop started ending: `pulselen =
/// None` ahead of `pulselen = self._echo[0]`, and `timestamp = time.monotonic()` inside a
/// method that reaches through a field. The second made every measurement after the first time
/// out at once, because an elapsed time measured from 0 is already past any timeout.
/// </summary>
[TestFixture]
public class NoneThenValueWidthTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("none-then-value-width"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("none-then-value-width"));
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
        "fn-none 570", "fn-plain 570", "mod-none 570", "held-none 570", "held-plain 570",
        "held-float 105", "held-after 570", "through-wide 570", "through-float 105", "END",
    };

    [Test]
    public void NothingIsTruncated()
        => Lines(_session).Should().Equal(Cpython,
            "570 is the value stored; 58 is its low byte");

    [Test]
    public void TheSameThroughThePythonFrontEnd()
        => Lines(_pySession).Should().Equal(Cpython);

    [Test]
    public void ANoneBindingDoesNotDecideTheWidth()
        => Lines(_session).Should().ContainInOrder(
            new[] { "fn-none 570", "mod-none 570", "held-none 570" },
            "in a function, at module level and inside an expanded method");

    [Test]
    public void AFreshLocalInAnExpandedBodyIsAsWideAsWhatItHolds()
        => Lines(_session).Should().ContainInOrder(
            new[] { "through-wide 570", "through-float 105" },
            "no None involved: reaching through a field is what forces the expansion");
}
