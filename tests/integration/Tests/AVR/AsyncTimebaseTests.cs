// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// asyncio on AVR has a real time base: asyncio.ticks() reads the Timer0 micros
/// counter (armed by the millis_init() preamble the build driver injects for any
/// program containing an `async def`), so `await asyncio.sleep_ms(n)` suspends
/// for n real milliseconds. With the counter frozen at 0 the wait condition
/// `ticks() - start &lt; duration` never clears and gather() spins forever.
/// </summary>
[TestFixture]
public class AsyncTimebaseTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("async-timebase"));

    [Test]
    public void GatherCompletes_AndAwaitsTakeRealTime()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "T:OK\n", maxMs: 500);
        // T:OK is printed only when millis() measured 60..150 ms across gather();
        // six 10 ms awaits plus two 35 ms awaits land at ~70 ms.
        uno.Serial.Should().ContainLine("T:OK");
    }

    [Test]
    public void SlowCoroutineInterleavesWithTheFastOne()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "T:OK\n", maxMs: 500);
        // fast() fires at 10/20/30/40/50/60 ms, slow() at 35/70 ms.
        uno.Serial.Text.Should().Contain("ASY\nF\nF\nF\nS\nF\nF\nF\nS\n");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
