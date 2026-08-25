using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/counter-in-loop (PyMCU#114).
///
/// The most elementary object in programming, a counter, printed 1, 1, 1. A loop body is
/// emitted once and executed many times, and the expansion folded the field against the
/// value it held on the way in, so `self.n = self.n + 1` became `n = 1` and the call
/// disappeared into an empty outlined body. Clean build, no diagnostic.
///
/// The sum is checked as well as the sequence: three prints of "1" and a total of 3 would
/// otherwise satisfy a test that only looked for the digits.
/// </summary>
[TestFixture]
public class CounterInLoopTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("counter-in-loop"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        return uno;
    }

    [Test]
    public void TheCounterAdvances()
    {
        Boot().Serial.Text.Should().Contain("1\n2\n3\n", "each call must see the field the previous one left");
    }

    [Test]
    public void TheValuesActuallyAccumulate()
    {
        Boot().Serial.Text.Should().Contain("sum 6\n", "1 + 2 + 3, not 1 + 1 + 1");
    }
}
