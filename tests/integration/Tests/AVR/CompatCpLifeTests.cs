// SPDX-License-Identifier: MIT
using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using AVR8Sharp.Core.Peripherals;
using FluentAssertions;
using NUnit.Framework;
using PyMCU.TestKit;
using System.Security.Cryptography;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The Game of Life demandant: the unmodified Adafruit SSD1306 + framebuf pair
/// drives Conway's Life on a 32x8 grid of 4x4 cells -- two bytearray(256) cell
/// buffers plus the driver's 513-byte framebuffer, on an ATmega328P with 2048
/// bytes of SRAM. Two storage bugs made this not fit, and the fixture pins both
/// fixes against real wire traffic:
///
///   * <c>display_buffer</c> was emitted twice: once as a 513-byte contiguous
///     array and once as 513 scalar <c>display_buffer__N</c> slots no instruction
///     referenced (the inlined <c>i2c_device.write(buffer)</c> burst unrolled
///     into slot Variables). <see cref="Framebuffer_IsAllocatedOnce"/> greps the
///     listing so the duplication cannot come back.
///   * the nested form (<c>for y in range(8): for x in range(32)</c>) was
///     unrolled at trip count 8 regardless of the body, which multiplied the
///     inner loop's inlined <c>fill_rect</c> past the SRAM budget. The unroll
///     policy is cost-aware now: the outer loop stays a counter loop.
///     <see cref="CompatCpLifeNestedTests"/> compiles that shape and proves the
///     generations it computes are identical on the wire.
///
/// Same oracle shape as <see cref="CompatCpFramebufTextTests"/>: the fixture's
/// oracle.py runs the identical vendored sources under CPython against a fake
/// I2C bus -- with <c>time.sleep</c> faked to a no-op and a sentinel exception
/// that ends the program when the last expected framebuffer write is recorded,
/// since the program's tail is <c>while True: pass</c>. The stream is
/// 29 + 7 * (generations + 2) transactions: the zero-length probe, the init
/// command burst, then six address-window command writes plus one 513-byte
/// framebuffer write per shown frame (the init's own show, the seed draw and
/// one draw per generation).
/// </summary>
public abstract class CompatCpLifeBase(
    string fixture, int generations, double avrMaxMs = 20_000)
{
    protected const byte OledAddr = 0x3C;
    private const int FrameBytes = 513;
    private readonly int _generations = generations;
    protected readonly double _avrMaxMs = avrMaxMs;

    // md5 of each vendored file, pinned so an accidental edit fails the test.
    private static readonly (string RelPath, string Md5)[] VendoredFiles =
    {
        ("src/adafruit_ssd1306.py",              "d70a9d7eb565ebf6f4ff595ee9076956"), // Adafruit_CircuitPython_SSD1306 tag 2.12.24
        ("src/adafruit_framebuf.py",             "f8a793f779537106702c42cb8569dcd1"), // PyPI adafruit-circuitpython-framebuf 1.6.12
        ("src/adafruit_bus_device/__init__.py",  "d41d8cd98f00b204e9800998ecf8427e"),
        ("src/adafruit_bus_device/i2c_device.py","2451d2fdd1ea8bf925976763e7781b4f"),
        ("src/adafruit_bus_device/spi_device.py","d78273b262908517d1425d0a5961dd21"),
        ("src/font5x8.bin",                      "221fa943a9d845f68ab2155c75d644fe"), // Adafruit_CircuitPython_framebuf font5x8.bin
    };

    protected readonly string Fixture = fixture;

    private string _hex = null!;
    private string _pyHex = null!;
    private List<I2cTransaction> _oracle = null!;
    protected WireTrace _run = null!;

    // Sim-time (ms at 16 MHz) at which each recorded transaction completed --
    // the spacing of the 513-byte writes is the wall-clock of a generation.
    private List<double> _stampMs = null!;

    [OneTimeSetUp]
    public void BuildFirmwareAndRunOracle()
    {
        _hex    = PymcuCompiler.BuildFixture(Fixture);
        _pyHex  = PymcuCompiler.BuildFixturePyParser(Fixture);
        _oracle = OracleScript.Run(
            Path.Combine(PymcuCompiler.FixtureDir(Fixture), "oracle", "oracle.py"),
            Path.Combine(PymcuCompiler.Root, ".venv", "bin", "python"));
        (_stampMs, _run) = RecordWithStamps(_hex, _oracle.Count, _avrMaxMs);
    }

    // ── Tests ───────────────────────────────────────────────────────────────

    [Test]
    public void VendoredFiles_AreByteIdenticalToUpstream()
    {
        var root = PymcuCompiler.FixtureDir(Fixture);
        foreach (var (rel, expectedMd5) in VendoredFiles)
        {
            var path = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(path).Should().BeTrue($"vendored file {rel} must exist in the fixture");
            var actual = Convert.ToHexString(MD5.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
            actual.Should().Be(expectedMd5,
                $"{rel} must stay byte-identical to upstream, or the oracle no longer measures the unmodified driver");
        }
    }

    [Test]
    public void Framebuffer_IsAllocatedOnce()
    {
        // The listing is a build artifact of BuildFixture above. The demandant
        // bug emitted `display_buffer` both as one contiguous array and as 513
        // scalar slots no instruction referenced; exactly one .equ must remain.
        var asmPath = Path.Combine(
            PymcuCompiler.FixtureDir(Fixture), "dist", "firmware.gas.asm");
        File.Exists(asmPath).Should().BeTrue($"{asmPath} must be left by the fixture build");
        var asm = File.ReadAllText(asmPath);
        var scalarSlots = System.Text.RegularExpressions.Regex.Matches(
            asm, @"\.equ\s+display_buffer__\d+").Count;
        scalarSlots.Should().Be(0,
            "the framebuffer must have exactly one storage: a contiguous array, " +
            "not a second set of per-byte scalar slots");
        System.Text.RegularExpressions.Regex.Matches(asm, @"\.equ\s+display_buffer,")
            .Count.Should().Be(1, "the contiguous framebuffer array itself must appear once");
    }

    [Test]
    public void Life_Generations_SendTheSameI2cTrafficAsCPython()
    {
        I2cStreams.AssertEqual(_oracle, _run, "C# front end");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        var run = UnoTwiTrace.Record(_pyHex, OledAddr, _oracle.Count, maxMs: _avrMaxMs);
        I2cStreams.AssertEqual(_oracle, run, "Python front end");
    }

    [Test]
    public void BothFrontEnds_ProduceIdenticalFirmware()
    {
        _pyHex.Should().Be(_hex,
            "both front ends must compile the same sources to byte-identical firmware");
    }

    [Test]
    public void Generations_EvolveAndCostIsReported()
    {
        // Every 513-byte transaction is one show(): __init__'s empty show, the
        // seed draw, then GENERATIONS loop draws -- GENERATIONS+2 in total.
        var frames = _oracle
            .Select((t, i) => (t, i))
            .Where(p => p.t.IsWrite && p.t.Data.Length == FrameBytes)
            .ToList();
        frames.Count.Should().Be(_generations + 2,
            "the wire carries __init__'s show, the seed draw, and GENERATIONS evolutions");
        var payloads = frames.Select(p => Convert.ToHexString(p.t.Data)).ToList();
        payloads.Distinct().Count().Should().BeGreaterThan(2,
            "the grid must visibly evolve between generations -- not one frozen frame");

        for (var f = 1; f < frames.Count; f++)
        {
            var ms = _stampMs[frames[f].i] - _stampMs[frames[f - 1].i];
            TestContext.Progress.WriteLine(
                $"[{Fixture}] generation {f}: {ms:F1} ms of emulated 16 MHz time");
        }
    }

    // ── stamped recorder ────────────────────────────────────────────────────

    /// <summary>
    /// Same recording as <see cref="UnoTwiTrace.Record"/> plus the sim time at
    /// which each transaction closed, so the per-generation cost is reportable.
    /// </summary>
    private static (List<double> StampMs, WireTrace Trace) RecordWithStamps(
        string hex, int stopAfterCount, double maxMs)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        // A real ATmega328P's R2-R15 pool and SRAM are undefined at cold
        // boot; avr8sharp's are always zero. This family is exactly where
        // that gap mattered (PyMCU-gemlife-lockinit): poison before running
        // so this suite keeps demonstrating the firmware does not depend on
        // it, instead of demonstrating only that the emulator is generous.
        ColdBootRealism.Poison(uno, 0xFF);
        // Same as UnoTwiTrace.Record: the GPIO model does not drive PIN from
        // the internal pull-ups, so hold SDA/SCL high like a wired bus at idle,
        // or CircuitPython's busio.I2C wiring check refuses the bus.
        uno.PortC.SetPinValue(4, true);   // SDA
        uno.PortC.SetPinValue(5, true);   // SCL
        uno.AddTwi(AvrTwi.TwiConfig, out var twi);
        var recorder = new TwiRecorder(twi, OledAddr);
        twi.EventHandler = recorder;

        var stamps = new List<double>();
        string? crash = null;
        try
        {
            const double stepMs = 10.0;
            var elapsed = 0.0;
            while (recorder.Transactions.Count < stopAfterCount && elapsed < maxMs)
            {
                uno.RunMilliseconds(stepMs);
                elapsed += stepMs;
                while (stamps.Count < recorder.Transactions.Count)
                    stamps.Add(uno.Cpu.Cycles / 16000.0); // cycles -> ms @ 16 MHz
            }
            uno.RunMilliseconds(50);
            while (stamps.Count < recorder.Transactions.Count)
                stamps.Add(uno.Cpu.Cycles / 16000.0);
        }
        catch (Exception ex)
        {
            crash =
                $"simulation stopped abnormally: {ex.GetType().Name}: {ex.Message} " +
                $"at PC=0x{uno.Cpu.Pc:X4} (byte 0x{uno.Cpu.Pc * 2:X5}), " +
                $"SP=0x{uno.Cpu.Sp:X4}, SREG=0x{uno.Cpu.Sreg:X2}, " +
                $"cycles={uno.Cpu.Cycles}; {recorder.Transactions.Count} " +
                $"TWI transactions recorded before the stop";
            recorder.Flush();
        }
        return (stamps, new WireTrace(recorder.Transactions, crash));
    }
}

[TestFixture]
public class CompatCpLifeTests() : CompatCpLifeBase("compat-cp-life", generations: 8);

[TestFixture]
public class CompatCpLifeNestedTests() : CompatCpLifeBase("compat-cp-life-nested", generations: 8);

/// <summary>
/// The same Life on the same drivers, written the way a CircuitPython user
/// writes it: a <c>Life</c> class holding <c>self.width</c>/<c>self.height</c>
/// and the two bytearray grids, a <c>neighbours(x, y)</c> count over
/// <c>range(-1, 2)</c>, nested row/column loops everywhere, a Python LCG for
/// the seed (identical on every interpreter, unlike the random module), and
/// <c>time.monotonic()</c> pacing one generation every 0.1 s. 30 generations
/// so the "generation N" print every ten fires three times on the console.
/// </summary>
[TestFixture]
public class CompatCpLifeIdiomaticTests()
    : CompatCpLifeBase("compat-cp-life-idiomatic", generations: 30);

/// <summary>
/// The SAME Life program as <see cref="CompatCpLifeIdiomaticTests"/>, with the
/// cell grids written the way a CircuitPython user writes them:
/// <c>self.cells = [[0] * width for _ in range(height)]</c> and
/// <c>self.cells[y][x]</c>. The compiler lowers each grid to ONE flat
/// <c>uint8[256]</c> array, so the wire stream must be byte-identical to the
/// hand-flattened fixture's -- <see cref="GridFirmware_SendsTheSameI2cTrafficAsTheFlatSpelling"/>
/// records both on the emulator and compares transaction by transaction, and
/// <see cref="Grid_LowersToOneFlatArray"/> greps the listing so the lowering
/// cannot silently grow a row-object allocation.
/// </summary>
[TestFixture]
public class CompatCpLifeGridTests()
    : CompatCpLifeBase("compat-cp-life-grid", generations: 30)
{
    [Test]
    public void GridFirmware_SendsTheSameI2cTrafficAsTheFlatSpelling()
    {
        var flatHex = PymcuCompiler.BuildFixture("compat-cp-life-idiomatic");
        var flatRun = UnoTwiTrace.Record(flatHex, OledAddr, _run.Transactions.Count, maxMs: _avrMaxMs);
        _run.Transactions.Count.Should().Be(253,
            "30 generations plus init + seed draws must produce 253 I2C transactions");
        I2cStreams.AssertEqual(flatRun.Transactions.ToList(), _run,
            "list-of-rows vs hand-flattened bytearray");
    }

    [Test]
    public void Grid_LowersToOneFlatArray()
    {
        // Each grid must be ONE contiguous W*H allocation -- the same .equ the
        // flat bytearray spelling produces -- not W row objects.
        var asmPath = Path.Combine(
            PymcuCompiler.FixtureDir(Fixture), "dist", "firmware.gas.asm");
        var asm = File.ReadAllText(asmPath);
        foreach (var name in new[] { "life_cells", "life_next_cells" })
        {
            var equs = System.Text.RegularExpressions.Regex.Matches(
                asm, $@"\.equ\s+{name},");
            equs.Count.Should().Be(1, $"{name} must be one contiguous flat array");
            // A row-object lowering would need per-row storage symbols; the
            // flat lowering leaves exactly one symbol and nothing else.
            System.Text.RegularExpressions.Regex.Matches(asm, $@"{name}_row\d+")
                .Count.Should().Be(0, $"{name} must not allocate row objects");
        }
    }
}
