// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-introspection (PyMCU#266, RFC 0007): under
/// <c>stdlib = ["circuitpython"]</c> the driver passes <c>--stdlib</c> to pymcuc and
/// <c>if</c> conditions on <c>sys.implementation.name</c>, <c>.version[i]</c>,
/// <c>sys.platform</c> and <c>uname()</c> fold at compile time from the per-board table,
/// the way <c>__CHIP__</c> conditions already did.
///
/// WHAT DISCRIMINATES: the serial line shows the live arm of every guard, and
/// <c>dead_branch_marker:</c> -- whose only call site sat in the folded-away
/// <c>not ... == "circuitpython"</c> branch -- never became a symbol in firmware.asm.
/// A compile that left the conditions run-time would emit both.
/// </summary>
[TestFixture]
[Property("Issue", "266")]
public class CompatCpIntrospectionTests
{
    private const string Fixture = "compat-cp-introspection";

    private static SimSession _session = null!;
    private static SimSession _pySession = null!;
    private static string[] _lines = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture(Fixture));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser(Fixture));
        var asm = File.ReadAllText(Path.Combine(
            PymcuCompiler.FixtureDir(Fixture), "dist", "debug", "firmware.asm"));
        _lines = asm.Split('\n').Select(l => l.Trim()).ToArray();
    }

    private static string FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno.Serial.Text;
    }

    [Test]
    public void EveryIntrospectionGuardTookItsLiveArm()
    {
        FullRun(_session).Should().Be(
            "IMPL-CP\nVER-GE7\nPLAT-CHIP\nUNAME-CHIP\nNO-LINUX\nEND\n",
            because: "under the circuitpython layer on atmega328p: implementation.name is " +
            "\"circuitpython\", version[0] is 10, sys.platform is the chip name (no upstream " +
            "port), uname().sysname is the chip name, and no board is ever Linux");
    }

    [Test]
    public void DeadBranchFunctionNeverBecameASymbol()
        => _lines.Should().NotContain("dead_branch_marker:",
            because: "its only call site was inside `if not sys.implementation.name == " +
            "\"circuitpython\":`, which folds false on this layer -- a branch merely skipped " +
            "at run time would still have emitted the function it calls");

    [Test]
    public void TheSameThroughThePythonFrontEnd()
        => FullRun(_pySession).Should().Be(
            "IMPL-CP\nVER-GE7\nPLAT-CHIP\nUNAME-CHIP\nNO-LINUX\nEND\n",
            because: "the fold is on the AST, so both front ends produce the same image");
}
