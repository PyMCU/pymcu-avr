// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// RFC 0006 (docs/rfcs/0006-self-is-this.md, Section 12): this is pymcu.org's own
/// canonical blink, verbatim from website-copy's Playground.astro widget (also quoted
/// in FirmwareSizes.astro's "Why 142 bytes?" copy) -- the MicroPython layer,
/// <c>machine.Pin(13, Pin.OUT)</c>, <c>.toggle()</c>, <c>time.sleep_ms(500)</c>. A
/// single instance of a single Pin, so RFC 0006's fold rule (Section 5) leaves it
/// byte-identical: this fixture pins that number (142 B) to a real corpus entry
/// instead of an unmeasured reference in a design note.
///
/// Hardware: built-in LED on PB5 (Arduino Uno digital pin 13). No button, no UART.
/// Logic: led.toggle() -> sleep_ms(500) -> repeat, so PB5 flips every 500 ms starting
/// from LOW at boot (the first toggle happens almost immediately, before the first
/// sleep_ms call).
/// </summary>
[TestFixture]
public class CompatMpBlinkToggleTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("compat-mp-blink-toggle"));

    [Test]
    public void FlashSize_Is142Bytes()
    {
        // The exact number RFC 0006's gate (Section 12) pins by name: 40 bytes of user
        // code + 102 bytes of interrupt vector table. Read the same way `pymcu build`
        // reports it: the sum of every data record's byte count in the Intel HEX.
        FlashBytes(PymcuCompiler.BuildFixture("compat-mp-blink-toggle")).Should().Be(142,
            "this is the website's own canonical blink number (RFC 0006, Section 12); a " +
            "change here means either a real regression or the RFC's gate needs updating, " +
            "never a silent drift");
    }

    [Test]
    public void Led_TogglesHighWithinTheFirstHalfSecond()
    {
        var uno = Sim();
        // The first toggle() executes in the first few us, well before the first
        // sleep_ms(500) even starts counting.
        uno.RunMilliseconds(10);
        uno.PortB.Should().HavePinHigh(5);
    }

    [Test]
    public void Led_IsStillHighJustBeforeTheFirstSleepEnds()
    {
        var uno = Sim();
        uno.RunMilliseconds(400);
        uno.PortB.Should().HavePinHigh(5);
    }

    [Test]
    public void Led_GoesLowAfterTheFirstSleep()
    {
        var uno = Sim();
        // sleep_ms(500) + loop/toggle overhead finishes well before 600 ms.
        uno.RunMilliseconds(600);
        uno.PortB.Should().HavePinLow(5);
    }

    [Test]
    public void Led_IsStillLowJustBeforeTheSecondSleepEnds()
    {
        var uno = Sim();
        uno.RunMilliseconds(900);
        uno.PortB.Should().HavePinLow(5);
    }

    [Test]
    public void Led_GoesHighAgainAfterTheSecondSleep()
    {
        var uno = Sim();
        uno.RunMilliseconds(1100);
        uno.PortB.Should().HavePinHigh(5);
    }

    [Test]
    public void Led_CompletesThreeFullTogglesByOneAndAHalfSeconds()
    {
        var uno = Sim();
        // Three toggles (HIGH -> LOW -> HIGH -> LOW) land it LOW again by 1600 ms.
        uno.RunMilliseconds(1600);
        uno.PortB.Should().HavePinLow(5);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ArduinoUnoSimulation Sim() => _session.Reset();

    private static int FlashBytes(string hex)
    {
        int total = 0;
        foreach (var raw in hex.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith(":") || line.Length < 9) continue;
            int count = Convert.ToInt32(line.Substring(1, 2), 16);
            int recordType = Convert.ToInt32(line.Substring(7, 2), 16);
            if (recordType == 0) total += count;
        }
        return total;
    }
}
