// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/float-arg-after-int: a float argument that follows a narrower one must
/// land in the register window <c>AssignArgLocations</c> assigned it -- the same
/// window the callee's prologue reads.
///
/// The caller stages each float through R22:R25 on the stack and pops it into
/// place; the pop targets were fixed at arg0 -&gt; R22:R25 and arg1 -&gt; R18:R21.
/// R18:R21 is arg1's window only when arg0 is itself 4 bytes. Behind a 2-byte
/// arg0 the callee reads R20:R23, so <c>f(5, 36.5)</c> delivered 0x12 to the
/// low byte and 0x42 to the next -- the high half of the real value -- and the
/// callee printed 0.0. A float at position 2 was not even staged: the loop
/// stopped at k &lt;= 1, so the argument was never loaded.
///
/// WHAT DISCRIMINATES: the three printed numbers. A wrong window misplaces
/// bytes (36.5 reads as a denormal), a never-staged argument reads as whatever
/// the registers held, and a parameter never read in the body leaves the size
/// map guessing -- the third float's window then overlaps the R22:R25 that arg0
/// is really delivered in.
///
/// The expected lines are CPython's, running the same program.
/// </summary>
[TestFixture]
public class FloatArgAfterIntTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("float-arg-after-int"));

    private ArduinoUnoSimulation FullRun()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void AFloatBehindAnInt16LandsInItsWindow()
        => FullRun().Serial.Should().ContainLine("36.5",
            "the callee reads the float at R20:R23; the caller used to fill R18:R21");

    [Test]
    public void AFloatAtPositionTwoIsStillDelivered()
        => FullRun().Serial.Should().ContainLine("-1.25",
            "the stash loop stopped at k <= 1, so a register-assigned float arg2 was never loaded");

    [Test]
    public void AFloatBehindAnUnreferencedFloatLandsInItsWindow()
        => FullRun().Serial.Should().ContainLine("7.5",
            "an unread parameter has no size-map entry; understating it overlapped arg1's "
            + "window with the R22:R25 a float arg0 is really delivered in");
}
