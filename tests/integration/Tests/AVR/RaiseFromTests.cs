// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/raise-from (PyMCU#434): <c>raise X(...) from Y</c> is accepted and compiled
/// as <c>raise X(...)</c>.
///
/// There is no traceback and no <c>__cause__</c> on this target, so the two are
/// indistinguishable once compiled. Y is parsed and discarded. Found on unmodified
/// adafruit_irremote (<c>raise _IRRepeatException from None</c>) and on the usual
/// <c>raise TypeError(...) from err</c> wrapping of a bound handler name. Before this
/// the parser refused the form, so the fixture did not build at all.
///
/// WHAT DISCRIMINATES: the three messages <c>wrapped</c>, <c>cleared</c>, <c>plain</c>.
/// The first two carry a from-clause (a bound name, and None); the third is the same
/// raise without from. All three reach a handler that binds the NEW type and prints
/// <c>e.args[0]</c>. A from-clause that changed the payload, or a program that still
/// refused to build, would not print those three lines.
///
/// The expected lines are CPython's, running the same program.
/// </summary>
[TestFixture]
public class RaiseFromTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("raise-from"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("raise-from"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s, int maxMs = 5000)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "DONE\n", maxMs: maxMs);
        return uno;
    }

    [Test]
    public void RaiseFromABoundName_DeliversTheNewTypesMessage()
    {
        var uno = FullRun(_session);
        uno.Serial.Should().ContainLine("a:wrapped");
    }

    [Test]
    public void RaiseFromNone_DeliversTheNewTypesMessage()
    {
        var uno = FullRun(_session);
        uno.Serial.Should().ContainLine("b:cleared");
    }

    [Test]
    public void TheSameRaiseWithoutFrom_PrintsTheSameShape()
    {
        var uno = FullRun(_session);
        uno.Serial.Should().ContainLine("c:plain");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        var uno = FullRun(_pySession);
        uno.Serial.Text.Should().Contain("a:wrapped\nb:cleared\nc:plain\nDONE\n");
    }
}
