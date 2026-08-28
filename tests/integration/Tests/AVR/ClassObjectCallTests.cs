// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A method with no `self`, called through the class object, PyMCU/PyMCU#201. It used to emit
/// a call to a function the same build never defined, which surfaced at avr-ld as a symbol and
/// a byte offset with no line of the program in it.
///
/// fixtures/static-method covers the spelling that already worked, `@staticmethod @inline`,
/// where the body is expanded at the call site and no symbol is involved. This one covers the
/// undecorated method, which is compiled as a real subroutine, and it uses a class that has a
/// field and an ordinary instance method: the decision that used to send the method to
/// expansion-only turns on the field layout, so a class that has one is the harder case.
///
/// Assertions on what the program writes, because the values are the claim. 42 needs the
/// argument to arrive at all, 18 needs the receiver not to consume it, and 7 needs the
/// ordinary instance method to still read its own field.
/// </summary>
[TestFixture]
public class ClassObjectCallTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("class-object-call"));

    private string Transcript()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 500);
        return uno.Serial.Text;
    }

    [Test]
    public void CalledOnTheClassTheArgumentArrives()
    {
        Transcript().Should().Contain("CO\n42\n");
    }

    [Test]
    public void CalledOnAnInstanceTheReceiverDoesNotConsumeIt()
    {
        // The second half of the issue: the receiver was bound to the first parameter, so the
        // argument had nowhere to go and the build failed over a parameter declared one line
        // above the body that reads it.
        Transcript().Should().Contain("42\n18\n");
    }

    [Test]
    public void TheOrdinaryInstanceMethodStillReadsItsField()
    {
        Transcript().Should().Contain("18\n7\nEND\n");
    }
}
