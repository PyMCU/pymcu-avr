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
/// MEASURED. A zero bit is now inside the datasheet on all three of its numbers: 437 ns
/// high, 875 ns low, 1.31 us end to end. A one is high for 750 ns, which is in window, and
/// then low for 812 ns where the datasheet says 450, so its period is 1.56 us against 1.25.
///
/// It used to be 41 and 45 cycles for the two, because the emitter re-ran its `match bit`
/// inside the bit loop and every bit paid for the port dispatch again. That is fixed: there
/// is one emitter per pin now and nothing left to dispatch on. What is left of the gap is
/// three codegen shapes in the loop's tail, named in PyMCU#355, and the one-bit period is
/// pinned below at what it measures rather than asserted as though it were right.
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
    public void AZeroBitIsInsideTheDatasheetEndToEnd()
    {
        // The win. T0H, T0L and the period, all three, which was not true of any bit before
        // the per-pin emitter landed: a zero used to take 41 cycles.
        var (highs, lows, periods) = Pulses();

        // Seven of the eight: the last bit of a byte ends at a seam, whose low carries the
        // caller's loop as well and is measured separately.
        for (var i = 0; i < 7; i++)
        {
            (highs[i] * NsPerCycle).Should().BeInRange(250, 550, "T0H is 400 ns");
            (lows[i] * NsPerCycle).Should().BeInRange(700, 1000, "T0L is 850 ns");
            (periods[i] * NsPerCycle).Should().BeInRange(1100, 1400, "a bit is 1.25 us");
        }
    }

    [Test]
    public void AOneBitIsPinnedAtWhatItMeasures()
    {
        // T1H is in window; T1L is 812 ns where the datasheet says 450, so the period is
        // 25 cycles against a nominal 20. What is left is three codegen shapes in the
        // loop's tail, not this emitter's to spend -- PyMCU#355. Pinned at the measurement
        // so that fix shows up here as a test to update rather than passing unnoticed.
        var (highs, _, periods) = Pulses();

        for (var i = 8; i < 15; i++)
        {
            (highs[i] * NsPerCycle).Should().BeInRange(650, 950, "T1H is 800 ns");
            periods[i].Should().Be(25, "as measured; nominal is 20");
        }
    }

    [Test]
    public void TheByteSeamsCostTheCallAndNoMore()
    {
        // The last bit of a byte carries the caller's loop and the CALL into the emitter as
        // well. Separated from the bits so widening one band cannot hide the other.
        var periods = Pulses().Periods;

        new[] { periods[7], periods[15] }.Should().OnlyContain(p => p >= 34 && p <= 48,
            "39 and 43 cycles, as measured: 2.4 and 2.7 us, nowhere near the 50 us latch");
    }

    [Test]
    public void TheFrameIsFollowedByALatch()
    {
        var uno = AtTheFirstBreak();
        uno.RunInstructions(1);     // step off the BREAK, or the next run returns at once
        var start = uno.Cpu.Cycles;
        uno.RunToBreak(1_000_000);
        var elapsed = uno.Cpu.Cycles - start;

        // 24 bits at the measured rate is about 570 cycles; the reset that follows is more
        // than 50 us, which is another 800.
        elapsed.Should().BeGreaterThan(570 + 800,
            "the write ends by holding the line low past 50 us so the strip latches");
    }
}
