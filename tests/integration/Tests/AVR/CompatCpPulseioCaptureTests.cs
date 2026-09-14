using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-pulseio-capture (pymcu-circuitpython#9): pulseio.PulseIn measures the
/// pulses arriving on a pin. The module did not exist, so an infrared receiver and a DHT had
/// no way into the CircuitPython layer at all.
///
/// The test drives D2 itself: Timer1 runs free at prescaler 8 and a pin-change interrupt
/// timestamps every edge, so what the firmware reports back is the time between the edges
/// this test produced.
/// </summary>
[TestFixture]
public class CompatCpPulseioCaptureTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;
    private const int D2Bit = 2;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-pulseio-capture"));

    /// <summary>Drives D2 with the given alternating high/low durations, in microseconds,
    /// then returns what the firmware reported at each of its three reads.</summary>
    private static (byte A, byte B, byte C)[] Run(params int[] micros)
    {
        var uno = _session.Reset();
        uno.RunToBreak(40_000_000);

        // The firmware waits 40 ms here. Start low, then alternate.
        uno.PortD.SetPinValue(D2Bit, false);
        uno.RunCycles(1600);                 // 100 us of settling, low
        var level = true;
        foreach (var us in micros)
        {
            uno.PortD.SetPinValue(D2Bit, level);
            uno.RunCycles(us * 16);          // 16 cycles per microsecond at 16 MHz
            level = !level;
        }
        uno.PortD.SetPinValue(D2Bit, false);

        var reads = new (byte, byte, byte)[3];
        for (var i = 0; i < reads.Length; i++)
        {
            uno.RunInstructions(1);
            uno.RunToBreak(40_000_000);
            reads[i] = (uno.Data[Gpior0Addr], uno.Data[Gpior1Addr], uno.Data[Gpior2Addr]);
        }
        return reads;
    }

    private static int Word(byte lo, byte hi) => lo | (hi << 8);

    [Test]
    public void ThePulsesAreCountedAndMeasured()
    {
        // Four edges after the arming one: high 500, low 1500, high 500, low 1500.
        var r = Run(500, 1500, 500, 1500);
        r[0].A.Should().BeGreaterThanOrEqualTo(3, "four driven edges leave at least three intervals");
        Word(r[0].B, r[0].C).Should().BeInRange(495, 510, "the first pulse is 500 us");
        Word(r[1].B, r[1].C).Should().BeInRange(1490, 1515, "the second is 1500 us");
    }

    [Test]
    public void ShortAndLongPulsesAreToldApart()
    {
        // A DHT's two bit lengths: 26 us for a zero, 70 us for a one.
        var r = Run(26, 50, 70, 50);
        Word(r[0].B, r[0].C).Should().BeInRange(20, 34, "a 26 us bit");
        Word(r[1].B, r[1].C).Should().BeInRange(44, 58, "the 50 us gap after it");
    }

    [Test]
    public void PopleftTakesTheOldestAndShortensTheQueue()
    {
        var r = Run(500, 1500, 500, 1500);
        var lenBefore = r[0].A;
        Word(r[2].A, r[2].B).Should().BeInRange(495, 510, "popleft returns the first pulse");
        r[2].C.Should().Be((byte)(lenBefore - 2), "two pulses came out of the queue");
    }

    [Test]
    public void NothingOnThePinLeavesNothingToRead()
    {
        var r = Run();
        r[0].A.Should().Be(0);
        Word(r[0].B, r[0].C).Should().Be(0, "an index past the end reads as zero");
    }
}
