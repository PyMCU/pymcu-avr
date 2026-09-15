using System;
using System.Linq;
using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/raise-after-a-plain-call (PyMCU#384): a raise reaches the handler written for it,
/// whatever a call before it left in the T flag.
///
/// The exception model signals through the T flag, and a `try` body reads it with a BRTS after
/// every call it contains, because the callee's CanFail is not known when the body is lowered.
/// T is not part of the AVR calling convention, and libgcc's soft-float routines carry a sign
/// bit through BST/BLD and return with it in whatever state their last operation left.
///
/// So printing a float armed the guard that followed the `time.sleep()` inside the method, the
/// raise below it was never reached, the dispatcher ran with a stale code in the error register
/// and the program halted with `E:RuntimeError` while the handler sat three lines away.
///
/// Found on a real Uno with the unmodified `adafruit_hcsr04`, whose `_dist_two_wire` sleeps
/// before its timeout raise and whose caller reads a float field first.
/// </summary>
[TestFixture]
public class RaiseAfterAPlainCallTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("raise-after-a-plain-call"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("raise-after-a-plain-call"));
    }

    private static string[] Lines(SimSession s)
    {
        var uno = s.Reset();
        uno.RunMilliseconds(400);
        return uno.Serial.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                              .Select(l => l.Trim()).ToArray();
    }

    private static readonly string[] Cpython = { "0.1", "Retrying!", "END" };

    [Test]
    public void TheHandlerRunsAndTheProgramGoesOn()
        => Lines(_session).Should().Equal(Cpython,
            "the T flag the exception model signals with survives a call into the soft-float "
            + "runtime, which does not preserve it");

    [Test]
    public void TheSameThroughThePythonFrontEnd()
        => Lines(_pySession).Should().Equal(Cpython);
}
