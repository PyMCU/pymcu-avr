using System.Text.RegularExpressions;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// PyMCU#406. <c>pulseio.PulseIn(pin, maxlen=1)</c> used to reserve the HAL's fixed 128-pulse
/// ring regardless of what it asked for: 256 bytes of SRAM, 12.5 percent of an ATmega328P, for
/// a driver reading one pulse. The ring is now sized at compile time to the largest
/// <c>maxlen</c> any <c>PulseCapture</c> in the program asks for.
///
/// Not simulated: these fixtures only need to build. The assertion is on the static SRAM
/// span, read from <c>dist/firmware.gas.asm</c>'s <c>.equ _bss_end</c> -- the same measure
/// used to size the fix on the unmodified adafruit_hcsr04 example (PulseIn(echo_pin), the
/// default maxlen=2: 272 bytes before this fix, 20 after).
/// </summary>
[TestFixture]
public class PulseioMaxlenSramTests
{
    /// <summary>
    /// <c>.equ _bss_end, _stack_base + N</c> -- N is every byte of static SRAM the program
    /// declared, hardware stack excluded.
    /// </summary>
    private static int BssEnd(string fixtureName)
    {
        PymcuCompiler.BuildFixture(fixtureName);
        var asm = File.ReadAllText(Path.Combine(
            PymcuCompiler.FixtureDir(fixtureName), "dist", "firmware.gas.asm"));
        var m = Regex.Match(asm, @"\.equ _bss_end, _stack_base \+ (\d+)");
        m.Success.Should().BeTrue("firmware.gas.asm must declare _bss_end");
        return int.Parse(m.Groups[1].Value);
    }

    [Test]
    public void OnePulseInAtMaxlen1_SizesTheRingToOneEntry_NotTheOld128()
    {
        // One uint16 entry is 2 bytes. The old HAL allocated 256 bytes here (128 entries)
        // no matter what maxlen asked for, which is the bug this fixture reproduces.
        BssEnd("compat-cp-pulseio-maxlen-sram").Should().BeLessThan(20,
            "a single PulseIn(maxlen=1) needs a 2-byte ring, not the old fixed 256-byte one");
    }

    [Test]
    public void TwoPulseIn_SizeTheSharedRingToTheLargestMaxlenAsked()
    {
        // maxlen=4 and maxlen=16 on D8/D9 (PB0/PB1, one pin-change interrupt group) share the
        // one ring the HAL has: it must end the program at 16 entries (32 bytes), the larger
        // of the two, and never at 4 (the smaller would drop every pulse past the fourth on
        // D9) or at the old fixed 128 (256 bytes).
        BssEnd("compat-cp-pulseio-maxlen-sram-max").Should().BeLessThan(50,
            "two PulseIn sharing the ring take the largest maxlen asked (16 entries, 32 " +
            "bytes), not the old fixed 256-byte one");
    }
}
