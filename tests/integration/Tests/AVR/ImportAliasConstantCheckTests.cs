// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/import-alias-constant-check: pymcu-circuitpython's busio.py
/// <c>_i2c_check(rc)</c> shape end to end -- a renamed import of a plain
/// module-level constant (<c>from codes import OK_VAL as _OK, OTHER_VAL as
/// _OTHER</c>, the constants themselves assigned under
/// <c>if __CHIP__.arch == ...</c>, exactly like busio.py), compared inside
/// an if/elif with an implicit third path (no <c>else</c> in the real
/// shape; this fixture adds one only to tell "do nothing" apart from "ran
/// the wrong branch").
///
/// ResolveBindingLadder's "imported alias" rung re-keyed a MUTABLE global
/// through the alias's original name correctly, but only ever checked
/// mutableGlobals for it. A plain <c>OK_VAL = 10</c> is never a mutable
/// global, so the renamed names resolved to an uninitialized local instead
/// of the literal, and every branch below ran for every rc regardless of
/// its value.
///
/// WHAT DISCRIMINATES: <c>A</c>, <c>B</c>, <c>C</c>, END, each printed
/// exactly once, for exactly the rc that is supposed to trigger it.
/// </summary>
[TestFixture]
public class ImportAliasConstantCheckTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("import-alias-constant-check"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("import-alias-constant-check"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void EachBranchRunsForExactlyItsOwnRc()
    {
        FullRun(_session).Serial.Text.Should().Contain("A\nB\nC\nEND\n",
            because: "rc == _OTHER, rc != _OK (neither), and rc == _OK must each take " +
                     "their own branch, which needs _OTHER and _OK to actually hold 20 and " +
                     "10 -- not an uninitialized local every branch ran for");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("A\nB\nC\nEND\n",
            because: "both front ends must resolve the renamed import to the same constant");
    }
}
