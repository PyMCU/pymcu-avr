using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/avr/compat-cp-supervisor.
/// Verifies that supervisor.ticks_ms() from the pymcu-circuitpython compat
/// layer compiles and returns a small value shortly after boot. CircuitPython
/// does not promise ticks_ms() == 0 at boot (its documented guarantee is the
/// difference between two reads, not any one reading's absolute value), and
/// millis() -- which this stub now calls -- folds in the in-progress Timer0
/// overflow down to the cycle, so even the very first read can legitimately
/// see a handful of ticks already elapsed. The bound (&lt; 5) is generous
/// against that, not a tight timing assertion.
///
/// Fixture sends low byte of ticks_ms() result, then 0x44 ('D') done marker.
/// </summary>
[TestFixture]
public class CompatCpSupervisorTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-supervisor"));

    [Test]
    public void TicksMs_IsASmallValueShortlyAfterBoot()
    {
        var uno = Sim();
        uno.RunUntilSerialBytes(uno.Serial, 1, maxMs: 50);
        uno.Serial.Bytes[0].Should().BeLessThan(5,
            "ticks_ms() shortly after boot is small but not necessarily exactly 0 -- " +
            "CircuitPython promises the difference between reads, not this one's absolute value");
    }

    [Test]
    public void DoneMarker_ReceivedAfterTicksMs()
    {
        var uno = Sim();
        uno.RunUntilSerialBytes(uno.Serial, 2, maxMs: 50);
        uno.Serial.Bytes[1].Should().Be(0x44, "'D' done marker sent after ticks_ms()");
    }

    [Test]
    public void ExactOutput_SmallValueThenDone()
    {
        var uno = Sim();
        uno.RunUntilSerialBytes(uno.Serial, 2, maxMs: 50);
        uno.Serial.Bytes[0].Should().BeLessThan(5);
        uno.Serial.Bytes[1].Should().Be(0x44);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
