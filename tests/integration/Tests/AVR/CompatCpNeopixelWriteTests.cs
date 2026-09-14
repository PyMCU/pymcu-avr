using System.Collections.Generic;
using System.Linq;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-neopixel-write (pymcu-circuitpython#14): the CircuitPython
/// `neopixel_write(digitalinout, buf)` that a great many guides call directly, below the
/// `neopixel` library. The module did not exist, so that whole body of code stopped here.
///
/// The timing IS the protocol on a WS2812: there is no clock line, so a one is told from a
/// zero by how long the pin stays high. At 16 MHz one cycle is 62.5 ns. What the test reads
/// back is the waveform on PD6, decoded into the bytes the program asked to send, so a
/// swapped pulse width or a dropped bit fails as wrong data rather than as a number nobody
/// can check.
///
/// The frame is 0x00, 0xFF, 0xA5: the shortest pulse eight times, the longest eight times,
/// and an alternating byte that a decoder with the two widths swapped cannot read right.
///
/// MEASURED, and one number is not what it should be. The high times are inside the
/// protocol window and the data decodes, but the bit PERIOD is 41 cycles for a zero and 45
/// for a one, against a nominal 20: the emitter re-runs its `match bit` inside the bit loop,
/// so every bit pays for the port dispatch twice over. A strip still reads the frame, since
/// what ends one is the line staying low past 50 us and this is 2.1 us, but it goes out at
/// half rate. Pinned below at what it is, with the window it must not leave. See PyMCU#354.
/// </summary>
[TestFixture]
public class CompatCpNeopixelWriteTests
{
    private const int DdrdAddr  = 0x2A;
    private const int PortdAddr = 0x2B;
    private const int DataBit   = 6;        // board.D6 is PD6

    private const double NsPerCycle = 62.5; // 16 MHz

    // WS2812B, from the datasheet: T0H 400 ns, T1H 800 ns, each 150 ns either way.
    private const double ZeroHighMinNs = 250, ZeroHighMaxNs = 550;
    private const double OneHighMinNs  = 650, OneHighMaxNs  = 950;

    // What ends a frame. Anything shorter than this between bits is just a slow frame;
    // reaching it mid-frame would make the strip latch half a frame and show it.
    private const double LatchNs = 50_000;

    private static readonly byte[] Frame = { 0x00, 0xFF, 0xA5 };

    private static string _hex = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _hex = PymcuCompiler.BuildFixture("compat-cp-neopixel-write");

    private static ArduinoUnoSimulation AtTheFirstBreak()
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(_hex);
        uno.RunToBreak(1_000_000);
        return uno;
    }

    /// <summary>Cycles the pin was high for each pulse, and cycles from one pulse to the next.</summary>
    private static (List<long> Highs, List<long> Lows, List<long> Periods) Pulses()
    {
        var uno = AtTheFirstBreak();

        var edges = new List<long>();
        var previous = (uno.Data[PortdAddr] >> DataBit) & 1;
        var start = uno.Cpu.Cycles;

        // Instruction by instruction is as fine as this simulation samples, and inside the
        // bit loop an instruction is one or two cycles.
        for (var i = 0; i < 60_000; i++)
        {
            uno.RunInstructions(1);
            var level = (uno.Data[PortdAddr] >> DataBit) & 1;
            if (level == previous) continue;
            edges.Add(uno.Cpu.Cycles - start);
            previous = level;
        }

        // The line starts low, so the edges alternate rising, falling, rising, ...
        var highs = new List<long>();
        var lows = new List<long>();
        var periods = new List<long>();
        for (var i = 0; i + 1 < edges.Count; i += 2)
        {
            highs.Add(edges[i + 1] - edges[i]);
            if (i + 2 >= edges.Count) continue;
            lows.Add(edges[i + 2] - edges[i + 1]);
            periods.Add(edges[i + 2] - edges[i]);
        }
        return (highs, lows, periods);
    }

    /// <summary>The frame as a strip would read it: the longer of the two pulses is a one.</summary>
    private static byte[] Decode(IReadOnlyList<long> highs)
    {
        const double midpointNs = (400 + 800) / 2.0;
        var bytes = new List<byte>();
        for (var i = 0; i + 7 < highs.Count; i += 8)
        {
            var value = 0;
            for (var bit = 0; bit < 8; bit++)
                value = (value << 1) | (highs[i + bit] * NsPerCycle > midpointNs ? 1 : 0);
            bytes.Add((byte)value);
        }
        return bytes.ToArray();
    }

    [Test]
    public void TheDataPinIsDrivenBeforeAnythingIsSent()
    {
        var uno = AtTheFirstBreak();

        (uno.Data[DdrdAddr] & (1 << DataBit)).Should().Be(1 << DataBit, "PD6 is an output");
        (uno.Data[PortdAddr] & (1 << DataBit)).Should().Be(0,
            "the line is held low before a frame: a high line reads as the front of a bit");
    }

    [Test]
    public void EveryBitOfTheFrameReachesTheWire()
    {
        Pulses().Highs.Should().HaveCount(Frame.Length * 8,
            "three bytes, eight pulses each, no bit dropped or doubled");
    }

    [Test]
    public void TheWaveformDecodesToTheBytesThatWereSent()
    {
        Decode(Pulses().Highs).Should().Equal(Frame,
            "0xA5 alternates, so a decoder with the two widths swapped cannot read it right");
    }

    [Test]
    public void TheZeroPulsesAreInsideTheProtocolWindow()
    {
        // The first byte is 0x00: eight of the short pulse. Measured 7 cycles = 437 ns.
        foreach (var high in Pulses().Highs.Take(8))
            (high * NsPerCycle).Should().BeInRange(ZeroHighMinNs, ZeroHighMaxNs,
                "T0H is 400 ns with 150 ns either way");
    }

    [Test]
    public void TheOnePulsesAreInsideTheProtocolWindow()
    {
        // The second byte is 0xFF: eight of the long pulse. Measured 12 cycles = 750 ns.
        foreach (var high in Pulses().Highs.Skip(8).Take(8))
            (high * NsPerCycle).Should().BeInRange(OneHighMinNs, OneHighMaxNs,
                "T1H is 800 ns with 150 ns either way");
    }

    [Test]
    public void TheTwoPulseWidthsAreToldApartByMoreThanTheSamplingError()
    {
        var highs = Pulses().Highs;
        var shortest = highs.Take(8).Max();
        var longest = highs.Skip(8).Take(8).Min();

        (longest - shortest).Should().BeGreaterThanOrEqualTo(4,
            "a strip decides on this gap; anything narrower is within the noise of one instruction");
    }

    [Test]
    public void NoGapInsideTheFrameReachesTheLatchTime()
    {
        // This is the one that would corrupt a frame rather than slow it: a low long enough
        // to look like the end of one makes the strip show whatever it has so far.
        foreach (var low in Pulses().Lows)
            (low * NsPerCycle).Should().BeLessThan(LatchNs,
                "a gap this long inside a frame is read as end-of-frame");
    }

    [Test]
    public void TheBitPeriodIsPinnedAtWhatItMeasures()
    {
        // NOT the nominal 1.25 us, and deliberately not asserted as though it were. The
        // emitter re-runs its port dispatch inside the bit loop, so a bit costs 41 cycles
        // (2.56 us) for a zero and 45 (2.81 us) for a one. The frame is valid at that rate
        // and goes out at half speed; PyMCU#354 is the fix. This pins the number so the fix
        // shows up here as a failure to update rather than passing unnoticed.
        var periods = Pulses().Periods;

        // Two of them are byte seams -- the caller's loop and the call into the emitter --
        // and cost 63 and 68 cycles. Separated so widening one band cannot hide the other.
        var seams = new[] { periods[7], periods[15] };
        var withinAByte = periods.Where((_, i) => i != 7 && i != 15);

        withinAByte.Should().OnlyContain(p => p >= 38 && p <= 48,
            "41 cycles for a zero, 45 for a one, as measured (nominal is 20)");
        seams.Should().OnlyContain(p => p >= 58 && p <= 72,
            "the gap between two bytes carries the loop and the call as well");
    }

    [Test]
    public void TheFrameIsFollowedByALatch()
    {
        var uno = AtTheFirstBreak();
        uno.RunInstructions(1);     // step off the BREAK, or the next run returns at once
        var start = uno.Cpu.Cycles;
        uno.RunToBreak(1_000_000);
        var elapsed = uno.Cpu.Cycles - start;

        // 24 bits at the measured rate is about 1030 cycles; the reset that follows is more
        // than 50 us, which is another 800.
        elapsed.Should().BeGreaterThan(1030 + 800,
            "the write ends by holding the line low past 50 us so the strip latches");
    }
}
