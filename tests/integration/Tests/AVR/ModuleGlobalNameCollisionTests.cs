using System;
using System.Linq;
using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/module-global-name-collision (PyMCU#381): a user's own import does not change how a
/// layer function's parameters bind.
///
/// `alarm.py` names its module-level singleton `time`, to give CircuitPython's
/// `alarm.time.TimeAlarm` spelling. A user's plain `import time` against that name was enough to
/// file `alarm` itself as an instance, so the dotted call `alarm.sleep_until_alarms(ta, pa)` was
/// given the receiver offset a method gets and bound its first argument to the SECOND parameter.
/// `alarm0` was bound to nothing, and the first read of it inside the library was reported as a
/// name that is not defined -- naming a parameter written in the signature two lines above.
///
/// The callee has no `self`, which is the one fact that settles the offset whatever put it there.
/// </summary>
[TestFixture]
public class ModuleGlobalNameCollisionTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("module-global-name-collision"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("module-global-name-collision"));
    }

    private static string[] Lines(SimSession s)
    {
        var uno = s.Reset();
        uno.RunMilliseconds(400);
        return uno.Serial.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                              .Select(l => l.Trim()).ToArray();
    }

    private static readonly string[] Cpython = { "0", "done" };

    [Test]
    public void TheFirstArgumentReachesTheFirstParameter()
        => Lines(_session).Should().Equal(Cpython,
            "a module function reached through a dotted name has no self to receive, so the "
            + "call must not consume a receiver slot");

    [Test]
    public void TheSameThroughThePythonFrontEnd()
        => Lines(_pySession).Should().Equal(Cpython);
}
