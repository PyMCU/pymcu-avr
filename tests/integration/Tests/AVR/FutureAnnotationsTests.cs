// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/future-annotations (PyMCU#452): <c>from __future__ import annotations</c>
/// is a compiler pragma that enables nothing here -- annotations are already read
/// from the source.
///
/// CircuitPython libraries (adafruit_irremote among them) write it unguarded at
/// the top of the file. Before this, DependencyGraphBuilder tried to LOAD
/// <c>__future__</c> like any third-party module and refused with "Module not
/// found: __future__", so the fixture did not build at all.
///
/// WHAT DISCRIMINATES: 9, <c>add(8, 1)</c>. A program that still refused the
/// import would not reach END.
/// </summary>
[TestFixture]
public class FutureAnnotationsTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("future-annotations"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("future-annotations"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 1000);
        return uno;
    }

    [Test]
    public void FromFutureImportAnnotations_BuildsAndTheTypedFunctionRuns()
    {
        FullRun(_session).Serial.Text.Should().Contain("9\nEND\n");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("9\nEND\n");
    }
}
