using System.Collections.Generic;
using System.Linq;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-neopixel: the unmodified upstream `neopixel` and
/// `adafruit_pixelbuf` sources, end to end. `pixels[i] = (r, g, b)` exercises
/// __setitem__'s isinstance guards, _parse_color's int/tuple split,
/// PixelBuf.__init__'s compile-time byteorder parsing ("GRB".index, "W" in
/// byteorder, the tuple return), and the arena-backed frame buffer out through
/// CircuitPython's own neopixel_write shim.
///
/// What the test reads back is the waveform on PD6, decoded into bytes the way
/// the sibling compat-cp-neopixel-write fixture does: a WS2812 tells a one from a
/// zero by how long the pin stays high, so a swapped or dropped byte fails as
/// wrong data.
///
/// GRB order puts green first, so `pixels[0] = (1, 2, 3)` writes G=2, R=1, B=3
/// into the frame buffer's first three bytes -- the wire frame for pixel 0 is
/// 0x02, 0x01, 0x03 and the other three pixels are zeros.
/// </summary>
[TestFixture]
public class CompatCpNeopixelTests
{
    private const int DdrdAddr  = 0x2A;
    private const int PortdAddr = 0x2B;
    private const int DataBit   = 6;        // board.D6 is PD6

    // The AVR BREAK opcode; the sim halts checkpoints by sitting on it.
    private const ushort BreakOpcode = 0x9598;

    private const double NsPerCycle = 62.5; // 16 MHz

    private const int Pixels = 4;
    private const int BytesPerPixel = 3;

    // Frame 1 is fill(0): every byte zero. Frame 2 is pixels[0] = (1, 2, 3) in
    // GRB order: green first, so 2, 1, 3 and then zeros.
    private static readonly byte[] BlankFrame = new byte[Pixels * BytesPerPixel];
    private static readonly byte[] LitFrame =
        { 0x02, 0x01, 0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    private static string _hex = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _hex = PymcuCompiler.BuildFixture("compat-cp-neopixel");

    private static bool AtBreak(ArduinoUnoSimulation uno)
        => uno.Cpu.ProgramMemory[uno.Cpu.Pc] == BreakOpcode;

    /// <summary>Pulse high-times from the sim's current position until the next BREAK.</summary>
    private static List<long> PulsesUntilBreak(ArduinoUnoSimulation uno, int maxInstructions)
    {
        var edges = new List<long>();
        var previous = (uno.Data[PortdAddr] >> DataBit) & 1;
        var start = uno.Cpu.Cycles;

        for (var i = 0; i < maxInstructions; i++)
        {
            // Step first: a call that starts on the previous BREAK must move off it.
            uno.RunInstructions(1);
            if (AtBreak(uno)) break;
            var level = (uno.Data[PortdAddr] >> DataBit) & 1;
            if (level == previous) continue;
            edges.Add((long)(uno.Cpu.Cycles - start));
            previous = level;
        }

        // The line starts low, so the edges alternate rising, falling, rising, ...
        // and the high time of each pulse is edges[i+1] - edges[i].
        var highs = new List<long>();
        for (var i = 0; i + 1 < edges.Count; i += 2)
            highs.Add(edges[i + 1] - edges[i]);
        return highs;
    }

    /// <summary>The frame as a strip would read it: the longer of the two pulses is a one.</summary>
    private static byte[] Decode(IReadOnlyList<long> highs, int byteCount)
    {
        const double midpointNs = (400 + 800) / 2.0;
        var bytes = new List<byte>();
        for (var i = 0; i + 7 < highs.Count && bytes.Count < byteCount; i += 8)
        {
            var value = 0;
            for (var bit = 0; bit < 8; bit++)
                value = (value << 1) | (highs[i + bit] * NsPerCycle > midpointNs ? 1 : 0);
            bytes.Add((byte)value);
        }
        return bytes.ToArray();
    }

    private static ArduinoUnoSimulation Boot()
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(_hex);
        return uno;
    }

    [Test]
    public void TheDataPinIsDrivenBeforeAnythingIsSent()
    {
        var uno = Boot();
        uno.RunToBreak(2_000_000);

        (uno.Data[DdrdAddr] & (1 << DataBit)).Should().Be(1 << DataBit, "PD6 is an output");
        (uno.Data[PortdAddr] & (1 << DataBit)).Should().Be(0,
            "the line is held low before a frame: a high line reads as the front of a bit");
    }

    [Test]
    public void FillBlanksEveryPixelOnTheWire()
    {
        var uno = Boot();
        var highs = PulsesUntilBreak(uno, 4_000_000);

        highs.Should().HaveCount(Pixels * BytesPerPixel * 8,
            "fill(0) with auto_write shows one frame: 4 pixels, 3 bytes, 8 bits each");
        Decode(highs, Pixels * BytesPerPixel).Should().Equal(BlankFrame,
            "fill(0) writes a zero to every byte of every pixel");
    }

    [Test]
    public void SetitemTupleWritesGrbBytesToPixelZero()
    {
        var uno = Boot();
        PulsesUntilBreak(uno, 4_000_000);                    // frame 1: fill(0)
        var highs = PulsesUntilBreak(uno, 4_000_000);        // frame 2: pixels[0] = (1,2,3)

        Decode(highs, Pixels * BytesPerPixel).Should().Equal(LitFrame,
            "GRB order sends green first: (1, 2, 3) is 0x02, 0x01, 0x03 on the wire, " +
            "and the other three pixels stay zero");
    }

    [Test]
    public void ThePythonFrontEndBuildsTheSameFirmware()
    {
        // This fixture exercises most of what the two front ends can disagree on --
        // isinstance folding, the byteorder string methods, the tuple-return unpack,
        // range(*slice.indices()), the try/except typing alias -- so the parity axis
        // matters here more than anywhere else in the suite.
        PymcuCompiler.BuildFixturePyParser("compat-cp-neopixel").Should().Be(_hex,
            "PYMCU_PY_PARSER=1 lowers the same AST to the same firmware image");
    }
}
