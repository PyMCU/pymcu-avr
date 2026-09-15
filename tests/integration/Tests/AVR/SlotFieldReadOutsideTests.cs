using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/slot-field-read-outside (PyMCU#409): a field of a boxed instance, read from
/// outside the class.
///
/// A class with two or more fields is boxed into a byte slot in SRAM, and reading one of its
/// fields from outside compiled to a read of a flattened `&lt;instance&gt;__&lt;field&gt;`
/// variable instead of a load from the slot. The mangling that claimed it exists to resolve
/// MODULE attribute access, and instance-field flattening spells its names the same way, so
/// `b._t` mangled to `b__t` -- a placeholder for which always exists -- and that branch
/// returned two hundred lines before the slot read could run.
///
/// Whether the flattened name held anything is what decided the answer. `A` initialises one
/// field from a LITERAL, so its constructor takes the materialising path through `__init__`,
/// which writes the flattened names on the way to the slot: read right, by accident. `B` takes
/// every field straight from a parameter, so the fast path writes the slot and only the slot:
/// read 0.
///
/// Both classes are here for that reason, and the one that was right is as much of the
/// measurement as the one that was wrong: a fix that broke `A` would be as wrong as the bug.
/// The method reads are the third control -- those always reached the slot, so the same field
/// printed two different values depending on how the program asked for it.
/// </summary>
[TestFixture]
public class SlotFieldReadOutsideTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("slot-field-read-outside"));

    /// The output as LINES, not as one string.
    ///
    /// A substring check cannot be used here: `m-b-n 7` contains `b-n 7`, so the assertion for
    /// the field that read 0 passed on the text printed by the METHOD read two lines below it.
    /// The bug was in the output and the test was green. Whole lines, so one checkpoint cannot
    /// answer for another.
    private static string[] Lines()
    {
        var uno = _session.Reset();
        uno.RunMilliseconds(400);
        return uno.Serial.Text.Split('\n').Select(l => l.Trim('\r', ' ')).ToArray();
    }

    // ── the class whose fields all come from parameters: read 0 before ──────────────────

    [Test]
    public void AFloatFieldReadFromOutsideAnswers()
        => Lines().Should().Contain("b-t 0.25",
            "the slot holds 0.25; the flattened name nothing ever wrote held 0.0");

    [Test]
    public void AnIntegerFieldReadFromOutsideAnswers()
        => Lines().Should().Contain("b-n 7", "it answered 0");

    // ── the class with a literal field initialiser: right before, and still right ───────

    [Test]
    public void TheClassThatWasRightByAccidentIsStillRight()
        => Lines().Should().Contain("a-t 0.1");

    [Test]
    public void ItsLiteralFieldIsStillRight()
        => Lines().Should().Contain("a-pad 3");

    // ── the path that always worked ────────────────────────────────────────────────────

    [Test]
    public void AFieldReadThroughAMethodIsUnchanged()
    {
        var lines = Lines();
        lines.Should().Contain("m-a-t 0.1");
        lines.Should().Contain("m-b-n 7");
    }

    [Test]
    public void TheProgramRunsToItsEnd() => Lines().Should().Contain("done");
}
