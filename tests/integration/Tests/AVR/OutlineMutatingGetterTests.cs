using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/outline-mutating-getter (PyMCU#95).
///
/// `@outline def bump(self): self.a = self.a + 1; return self.a` returned 6 and left the
/// instance holding 5. A Model A outlined method receives its field by value and returns
/// through the one return slot, which the returned expression already occupies, so the
/// assignment had nowhere to travel back through.
///
/// Both prints are checked: the returned value was never the wrong one, so a test that only
/// looked at it would have passed throughout the bug.
/// </summary>
[TestFixture]
public class OutlineMutatingGetterTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("outline-mutating-getter"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        return uno;
    }

    [Test]
    public void TheCallReturnsTheUpdatedValue()
    {
        Boot().Serial.Text.Should().StartWith("6\n", "5 + 1, and this half was always right");
    }

    [Test]
    public void TheInstanceKeepsWhatTheCallWrote()
    {
        Boot().Serial.Text.Should().Contain("6\n6\ndone\n", "reading the field after the call must not give back the 5");
    }
}
