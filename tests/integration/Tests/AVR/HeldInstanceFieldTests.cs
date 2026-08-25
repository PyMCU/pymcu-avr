using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/held-instance-field (PyMCU#119).
///
/// An object that holds another object and reads its field after calling its method printed
/// the field's initial value forever. The caller folded the value the field held at
/// construction, and the call passed the flattened field values where the slot body expected
/// a pointer to the instance, so the state the callee wrote landed at address zero.
///
/// The third line matters as much as the first two: it is the one that proves the inner
/// state advanced rather than the values being emitted from a table.
/// </summary>
[TestFixture]
public class HeldInstanceFieldTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("held-instance-field"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        return uno;
    }

    [Test]
    public void TheOuterSeesWhatTheInnerJustWrote()
    {
        Boot().Serial.Text.Should().Contain("7\n8\n", "the outer copies the inner's field after the call that sets it");
    }

    [Test]
    public void TheInnerStateAdvancesPastItsSecondBranch()
    {
        Boot().Serial.Text.Should().Contain("7\n8\n8\ndone\n",
            "the third poll returns 0 and leaves _value at 8, exactly as CPython does");
    }
}
