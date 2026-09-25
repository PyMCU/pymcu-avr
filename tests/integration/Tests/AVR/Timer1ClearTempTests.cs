using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for fixtures/timer1-clear-temp (PyMCU#492, the HAL half).
///
/// timer1_clear() cleared TCNT1 low byte first, the order a 16-bit READ takes. The low
/// byte's write is what commits an AVR 16-bit register pair, taking the high half from the
/// shared TEMP latch, so the counter came out of clear() holding the high byte of whatever
/// 16-bit access ran before it -- here the 0x0B that set_compare(0x0B34) left behind.
/// </summary>
[TestFixture]
public class Timer1ClearTempTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("timer1-clear-temp"));

    [Test]
    public void ClearLeavesTheCounterAtZeroWhateverTempHeld()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Be("0\ndone\n",
            "timer1_clear() has to write TCNT1H before TCNT1L; the other order commits " +
            "TEMP's stale high byte and reads back 2816 (0x0B00)");
    }
}
