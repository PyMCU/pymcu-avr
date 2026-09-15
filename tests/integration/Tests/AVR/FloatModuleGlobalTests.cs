using System;
using System.Linq;
using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/float-module-global (PyMCU#379): a module-level float keeps the value it was
/// written with.
///
/// The global scan typed a module-level binding from an INTEGER evaluation of its initializer,
/// and that evaluation answers for a float literal by truncating it: 0.1 gave 0 and 1.5 gave 1.
/// The name was then an integer, the module-level store folded to the same integer, and every
/// read came back as a small int. `timeout = 0.1` followed by `timeout * 1000000.0` printed
/// 0.0 while the same expression written as a literal printed 100000.0.
///
/// Found on a real Uno inside adafruit_hcsr04's wait loop, where the module-level timeout read
/// 0.0 and `0.001 > 0.0` ended the wait on the first millisecond tick (pymcu-avr#23).
///
/// Measured as text on the wire against what CPython prints for the same program. Four
/// spellings, two modules, with an int global and a literal as controls.
/// </summary>
[TestFixture]
public class FloatModuleGlobalTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("float-module-global"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("float-module-global"));
    }

    private static string[] Lines(SimSession s)
    {
        var uno = s.Reset();
        uno.RunMilliseconds(500);
        return uno.Serial.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                              .Select(l => l.Trim()).ToArray();
    }

    private static readonly string[] Cpython =
    {
        "timeout 100000.0",
        "TIMEOUT 100000.0",
        "literal 100000.0",
        "annotated 0.25",
        "negative -2.5",
        "count 7",
        "cfg.scale 0.25",
        "cfg.GAIN 1.5",
        "cfg.steps 9",
        "END",
    };

    [Test]
    public void EverySpellingKeepsItsValue()
        => Lines(_session).Should().Equal(Cpython,
            "a module-level float is storage of its own width, in the entry module and in an "
            + "imported one, whatever the name is spelled like");

    [Test]
    public void TheSameThroughThePythonFrontEnd()
        => Lines(_pySession).Should().Equal(Cpython);
}
