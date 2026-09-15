using System;
using System.Linq;
using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/instance-field-truthiness (PyMCU#385): a field holding an instance is true or false
/// the way its class says.
///
/// `if obj:`, `not obj` and `while not obj:` were rewritten to `__bool__` or `__len__` only when
/// the object was a NAME; a FIELD fell through to the numeric path and was decided by whatever
/// the instance collapsed to. `while not self._echo:` inside a method therefore ran forever
/// against an object whose `__len__` says it is not empty, which is the wait loop every
/// CircuitPython driver writes.
///
/// The second half is the outline decision: a method reading a field that HOLDS an instance
/// cannot be a shared body -- it receives the field as a number and has no `self` at all -- so
/// it is expanded at its call sites, where `self` is bound.
///
/// `poll()` covers re-evaluation: its `__len__` counts its own calls and answers 0, 0, then 1,
/// so a loop that keeps the first answer never leaves. CPython gives 2 then 0.
/// </summary>
[TestFixture]
public class InstanceFieldTruthinessTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("instance-field-truthiness"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("instance-field-truthiness"));
    }

    private static string[] Lines(SimSession s)
    {
        var uno = s.Reset();
        uno.RunMilliseconds(400);
        return uno.Serial.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                              .Select(l => l.Trim()).ToArray();
    }

    private static readonly string[] Cpython =
    {
        "len-true 1", "len-false 0", "bool-field 1", "name 1", "scalar 1",
        "poll-first 2", "poll-second 0", "END",
    };

    [Test]
    public void EveryTruthTestAsksTheClass()
        => Lines(_session).Should().Equal(Cpython,
            "__bool__ if the class defines one, else __len__, and a field holding a number is "
            + "still the number it is");

    [Test]
    public void TheSameThroughThePythonFrontEnd()
        => Lines(_pySession).Should().Equal(Cpython);
}
