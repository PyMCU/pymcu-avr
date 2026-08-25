using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/pwm-ocr-high-byte (PyMCU#163).
///
/// OCR1A and OCR1B are 16-bit, and every 16-bit timer register on the ATmega328P
/// commits through one shared TEMP byte: the write of the LOW byte writes TEMP as the
/// high byte. The PWM HAL used to write only OCR1AL, so the duty a Timer1 channel
/// actually received was TEMP:duty, where TEMP held whatever the last 16-bit write on
/// Timer1 left behind. A program that also drives Timer1 through the timer or servo
/// HAL therefore put OCR1A far above TOP, the compare never matched, and the channel
/// sat at 100% however little was asked for.
///
/// Why the register is read back through the CPU rather than sampled from the test:
/// OCR1AL alone reads back correct on the broken HAL too, because the low byte is the
/// one value that always lands. A test that checked the low byte would pass either
/// way. Only the committed 16-bit value distinguishes them, and reading it requires
/// the low byte first, which latches the high byte into TEMP.
///
/// Measured on both HALs, with 0x0BB7 (a 1500 us servo pulse) left in TEMP first:
///
///   duty     unfixed OCR1A     fixed OCR1A
///      1        2817                1
///     64        2880               64
///    128        2944              128
///    255        3071              255
///
/// Every one of these discriminates: there is no duty at which the two agree, because
/// the stale high byte is added to all of them.
///
/// Data-space addresses (ATmega328P):
///   GPIOR0 = 0x3E (seed in)   GPIOR1 = 0x4A (OCR1AH out)   GPIOR2 = 0x4B (OCR1AL out)
/// </summary>
[TestFixture]
public class PwmOcrHighByteTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;

    /// <summary>TOP for fast PWM 8-bit (WGM = 5), the mode the PWM HAL selects.</summary>
    private const int Top = 255;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("pwm-ocr-high-byte"));

    /// <summary>Boots with `duty` seeded and returns OCR1A as the CPU read it back.</summary>
    private static int CommittedOcr1A(byte duty)
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = duty;
        uno.RunToBreak();
        return uno.Data[Gpior1Addr] * 256 + uno.Data[Gpior2Addr];
    }

    [TestCase((byte)1)]
    [TestCase((byte)64)]
    [TestCase((byte)128)]
    [TestCase((byte)255)]
    public void TheDutyReachesTheCompareRegisterWithoutTheStaleHighByte(byte duty)
    {
        CommittedOcr1A(duty).Should().Be(duty,
            "OCR1A is committed as TEMP:duty, so the high byte has to be cleared " +
            "immediately before the low one; without that this reads 0x0B00 + duty");
    }

    [TestCase((byte)1)]
    [TestCase((byte)128)]
    [TestCase((byte)255)]
    public void TheCommittedDutyStaysWithinTop(byte duty)
    {
        CommittedOcr1A(duty).Should().BeLessThanOrEqualTo(Top,
            "a compare value above TOP never matches, so the output is never cleared " +
            "and the channel is stuck fully on regardless of the duty asked for");
    }
}
