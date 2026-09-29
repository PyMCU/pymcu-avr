// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// pymcu.org's own canonical CircuitPython blink, verbatim from README.md's "pitch in
/// one table" section: <c>digitalio.DigitalInOut(board.LED)</c>, <c>.direction =
/// Direction.OUTPUT</c>, <c>.value = True/False</c>, <c>time.sleep(0.5)</c>. Pinned here
/// the same way <see cref="CompatMpBlinkToggleTests"/> pins the MicroPython number, so
/// the README's size table has a real corpus entry behind it instead of a number nobody
/// rebuilds.
///
/// FlashSize_Is148Bytes is the regression test for the fix/b1-size size bug: before it,
/// this fixture built to 178 B. `import board` pulls in <c>busio.I2C/SPI/UART</c> (for
/// board.I2C() etc, never called here), and busio's error helpers raise OSError with a
/// message; the optimizer used to root the whole unhandled-exception report machinery
/// (__pymcu_exn_tail, __pymcu_print_exn_msg, two flash strings -- 30 B) by NAME ALONE,
/// whenever those two functions existed at all, rather than asking whether any message
/// store had actually survived dead-function elimination to a function the program still
/// reaches. digitalio.Direction.OUTPUT's setter clears PORT before DDR (the CircuitPython
/// spec requires it), so the correct number is 148 B: 2 bytes over the native-HAL and
/// MicroPython blinks (146 B each), not 32.
///
/// Hardware: built-in LED on PB5 (Arduino Uno digital pin 13). No button, no UART.
/// Logic: led.value=True -> sleep(0.5) -> led.value=False -> sleep(0.5) -> repeat, so
/// PB5 starts HIGH at boot (direction/value are set once, before the loop).
/// </summary>
[TestFixture]
public class CompatCpBlinkTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-blink"));

    [Test]
    public void FlashSize_Is148Bytes()
    {
        // Read the same way `pymcu build` reports it: the sum of every data record's
        // byte count in the Intel HEX. 146 B (native-HAL/MicroPython blink) + 2 B for
        // CircuitPython's PORT-before-DDR clear on Direction.OUTPUT.
        FlashBytes(PymcuCompiler.BuildFixture("compat-cp-blink")).Should().Be(148,
            "this is README.md's own canonical CircuitPython blink; a change here means " +
            "either a real regression (the unhandled-exception report machinery getting " +
            "rooted again for a message nothing reachable raises) or the README's table " +
            "needs updating, never a silent drift");
    }

    [Test]
    public void Led_StartsHighAfterBoot()
    {
        var uno = Sim();
        // direction=OUTPUT then value=True execute in the first few us.
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
        // sleep(0.5) + loop overhead finishes well before 600 ms.
        uno.RunMilliseconds(600);
        uno.PortB.Should().HavePinLow(5);
    }

    [Test]
    public void Led_GoesHighAgainAfterTheSecondSleep()
    {
        var uno = Sim();
        uno.RunMilliseconds(1100);
        uno.PortB.Should().HavePinHigh(5);
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
