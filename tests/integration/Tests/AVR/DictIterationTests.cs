// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Walking a dict literal, PyMCU/PyMCU#200. Every form was refused with a list of seven other
/// iterables that mentioned neither dicts nor sets; a dict binds a compile-time lookup table,
/// so the loop unrolls over its entries the way a constant list literal already does.
///
/// These read the serial output rather than the build, because the values are the claim. A
/// build-success assertion passes on a loop that unrolls in the wrong order, on a key bound as
/// the wrong constant, and on `codes[k]` folding to the wrong entry.
///
/// The whole transcript is the CPython transcript for the same program, checked line by line
/// against `python3` rather than derived from what the compiler emitted.
/// </summary>
[TestFixture]
public class DictIterationTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("dict-iteration"));

    private ArduinoUnoSimulation Sim() => _session.Reset();

    private string Transcript()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 500);
        return uno.Serial.Text;
    }

    /// <summary>
    /// `for k in codes` gives the KEYS, in the order written. Distinct from the values on
    /// purpose: a loop that walked the values instead would print 30 and 7 here.
    /// </summary>
    [Test]
    public void IteratingADictGivesItsKeysInOrder()
    {
        Transcript().Should().Contain("DI\n1\n2\n");
    }

    /// <summary>
    /// items() and values() over the same dict, then keys(). The three run back to back so the
    /// assertion also pins that one form does not consume or reorder the next.
    /// </summary>
    [Test]
    public void ItemsValuesAndKeysEachGiveTheirOwnHalf()
    {
        Transcript().Should().Contain("1\n2\n30\n7\n30\n7\n1\n2\n");
    }

    /// <summary>
    /// The key is a compile-time constant inside the body, which is what lets the second
    /// lookup fold. String keys are the shape a reader reaches for first, and the one the
    /// issue was filed with.
    /// </summary>
    [Test]
    public void TheKeyIsUsableAsAKeyInsideTheBody()
    {
        Transcript().Should().Contain("1\n2\n40\n2\n");
    }

    /// <summary>
    /// One-character keys, which are the case with two encodings: a one-character literal is
    /// its own character code in expression position and an interned id when read back through
    /// a name, and the two do not compare equal. The unrolled key is bound as both, so it is
    /// indistinguishable from the literal it stands for. `k = "a"` written by hand still is
    /// not, which is PyMCU/PyMCU#215.
    /// </summary>
    [Test]
    public void AOneCharacterKeyFindsItsOwnEntry()
    {
        Transcript().Should().Contain("40\n2\n50\n6\n");
    }

    /// <summary>
    /// `continue` inside an unrolled loop. Each iteration is emitted separately, so a continue
    /// has to reach the end of ITS OWN copy rather than the end of the last one: skipping key
    /// 1 leaves 7, and skipping nothing would leave 37.
    /// </summary>
    [Test]
    public void ContinueSkipsOneUnrolledIteration()
    {
        Transcript().Should().Contain("50\n6\n7\nEND\n");
    }
}
