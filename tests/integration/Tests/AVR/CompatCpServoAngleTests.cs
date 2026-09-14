using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-servo-angle (pymcu-circuitpython#8): the servo idiom, whole.
/// pwmio.PWMOut(board.D9, frequency=50) into adafruit_motor's Servo, and an angle that
/// lands on the pulse width it names.
///
/// Before: Timer1's eight-bit fast PWM has five frequencies and 50 Hz ran at 61, measured
/// as a 16 384 us period, while PWMOut.frequency reported the 50 that was asked for. The
/// duty had 256 counts of 64 us across the whole period, about 16 steps of angle.
/// </summary>
[TestFixture]
public class CompatCpServoAngleTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-servo-angle"));

    /// <summary>The compare value at each of the three angles, and the period low byte.</summary>
    private static (int Ocr, byte IcrLow)[] Run()
    {
        var uno = _session.Reset();
        var reads = new (int, byte)[3];
        for (var i = 0; i < reads.Length; i++)
        {
            if (i > 0) uno.RunInstructions(1);
            uno.RunToBreak(10_000_000);
            reads[i] = (uno.Data[Gpior0Addr] | (uno.Data[Gpior1Addr] << 8), uno.Data[Gpior2Addr]);
        }
        return reads;
    }

    [Test]
    public void ThePeriodIsExactlyTwentyMilliseconds()
    {
        // ICR1 = 39999 counts of 0.5 us. Its low byte is 39999 & 0xFF = 63.
        Run()[0].IcrLow.Should().Be(63, "39999 is 20 ms at 0.5 us a count, which is 50 Hz exactly");
    }

    [TestCase(0, 1999, 1000)]
    [TestCase(1, 2999, 1500)]
    [TestCase(2, 3999, 2000)]
    public void EachAngleLandsOnItsPulseWidth(int step, int ocr, int microseconds)
    {
        // The pin is high for OCR + 1 counts of 0.5 us.
        var r = Run()[step];
        r.Ocr.Should().Be(ocr, $"{microseconds} us is {microseconds * 2} counts");
        ((r.Ocr + 1) / 2).Should().Be(microseconds);
    }
}
