using System;
using System.Linq;
using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/print-order-effects (PyMCU#371): where a printed line's side effects happen.
///
/// CPython builds the whole line and writes it in one piece, so everything an operand does
/// happens before the first character of that line appears. This target streams the text, and
/// the literal parts went out before the operand between them ran: `print(f"a={side()}")`
/// gave `a=SIDE` then `7`, and `print((probe.reading,))` whose element raises had already put
/// the opening `(` on the wire, so the handler's own line arrived as `(Retrying!`.
///
/// Measured as TEXT ON THE WIRE against what CPython prints for the same program. The values
/// were never wrong; the interleaving was.
/// </summary>
[TestFixture]
public class PrintOrderEffectsTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("print-order-effects"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("print-order-effects"));
    }

    private static string[] Lines(SimSession s)
    {
        var uno = s.Reset();
        uno.RunMilliseconds(400);
        return uno.Serial.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                              .Select(l => l.Trim()).ToArray();
    }

    private static readonly string[] Cpython = { "SIDE", "a=7", "Retrying!", "done" };

    [Test]
    public void TheLinesAreTheOnesCPythonPrints()
        => Lines(_session).Should().Equal(Cpython,
            "a call inside a printed operand runs before the line it belongs to is written, "
            + "and a raise inside one leaves no half-written line behind");

    [Test]
    public void TheSameThroughThePythonFrontEnd()
        => Lines(_pySession).Should().Equal(Cpython);
}
