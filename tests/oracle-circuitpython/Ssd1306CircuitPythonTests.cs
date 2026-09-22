using FluentAssertions;
using NUnit.Framework;
using PyMCU.TestKit;
using System.Diagnostics;

namespace PyMCU.OracleCircuitPython;

/// <summary>
/// Three-way SSD1306 oracle: for one fixture program, three independent
/// executions must emit the same I2C transaction stream, byte for byte.
///
///   1. CPython     -- the fixture's oracle/oracle.py runs the vendored
///                     library sources under stock CPython against a fake I2C
///                     bus (the reference stream).
///   2. CircuitPython 10.3.1 on RP2040Sharp -- the same sources (same .py files
///                     installed into CIRCUITPY/lib, same program as code.py)
///                     running on the real interpreter on the emulated Pico.
///   3. PyMCU       -- the fixture compiled by this repo's backend and run on
///                     AVR8Sharp's emulated Uno.
///
/// The single permitted source difference is the I2C constructor: the
/// raspberry_pi_pico build of CircuitPython has no board.I2C(), so the harness
/// rewrites <c>i2c = board.I2C()</c> to <c>i2c = busio.I2C(board.GP1,
/// board.GP0)</c> in code.py only (SCL=GP1, SDA=GP0 -- the pins the wire
/// recorder decodes). Everything else -- driver sources, framebuffer content,
/// transaction order -- is identical by construction.
/// </summary>
public abstract class CircuitPythonOracleBase(string fixture, int expectedTransactions,
    bool runsForever = false, double avrMaxMs = 2_000)
{
    protected readonly string Fixture = fixture;
    protected readonly int ExpectedTransactions = expectedTransactions;

    /// <summary>
    /// True when the fixture program ends in <c>while True: pass</c>: the Pico
    /// run stops once <see cref="ExpectedTransactions"/> have been recorded
    /// instead of waiting for the REPL prompt that never comes.
    /// </summary>
    protected readonly bool RunsForever = runsForever;

    /// <summary>
    /// Sim-time ceiling for the AVR run; a program with real <c>time.sleep</c>
    /// calls between frames needs more than the 2 s default.
    /// </summary>
    protected readonly double AvrMaxMs = avrMaxMs;

    protected List<I2cTransaction> CPythonOracle = null!;
    protected CpRun CircuitPython = null!;
    protected WireTrace AvrFirmware = null!;

    [OneTimeSetUp]
    public async Task RunAllThree()
    {
        var sw = Stopwatch.StartNew();
        var snapshot = await CircuitPythonPico.SnapshotAsync();
        var snapshotMs = sw.ElapsedMilliseconds;

        var mainPy = File.ReadAllText(
            Path.Combine(Repo.FixtureDir(Fixture), "src", "main.py"));
        var codePy = CircuitPythonPico.ToCodePy(mainPy);

        sw.Restart();
        CircuitPython = CircuitPythonPico.RunAutorun(
            snapshot, codePy, RunsForever ? ExpectedTransactions : null);
        var cpMs = sw.ElapsedMilliseconds;

        CPythonOracle = OracleScript.Run(
            Path.Combine(Repo.FixtureDir(Fixture), "oracle", "oracle.py"),
            Repo.VenvPython);

        var hex = FixtureCompiler.BuildFixture(Fixture);
        AvrFirmware = UnoTwiTrace.Record(hex, address: 0x3C,
            stopAfterCount: CPythonOracle.Count, maxMs: AvrMaxMs);

        TestContext.Progress.WriteLine(
            $"[{Fixture}] snapshot {snapshotMs} ms | circuitpython run {cpMs} ms " +
            $"({CircuitPython.Transactions.Count} txns: hw={CircuitPython.HardwareCount}, " +
            $"bitbang={CircuitPython.BitbangCount}) | oracle {CPythonOracle.Count} txns | " +
            $"avr {AvrFirmware.Transactions.Count} txns");
    }

    [Test]
    public void CircuitPython_MatchesTheCpythonOracle()
    {
        CircuitPython.Transactions.Count.Should().Be(ExpectedTransactions,
            "the CircuitPython stream is measured against a pinned transaction count " +
            "recorded when this oracle was written -- a different count means the run " +
            "itself drifted, before any comparison");
        I2cStreams.AssertEqual(CPythonOracle,
            new WireTrace(CircuitPython.Transactions, null),
            $"CircuitPython {FirmwareCache.Version} on RP2040Sharp vs CPython oracle");
    }

    [Test]
    public void CircuitPython_MatchesTheAvrFirmware()
    {
        I2cStreams.AssertEqual(CircuitPython.Transactions, AvrFirmware,
            "PyMCU AVR firmware on AVR8Sharp vs CircuitPython "
            + FirmwareCache.Version + " on RP2040Sharp");
    }
}

[TestFixture]
public class AdafruitSsd1306OnCircuitPythonTests()
    : CircuitPythonOracleBase("adafruit-ssd1306-unmodified", expectedTransactions: 50);

[TestFixture]
public class CompatCpFramebufTextOnCircuitPythonTests()
    : CircuitPythonOracleBase("compat-cp-framebuf-text", expectedTransactions: 43);

// Long-string twin: 17- and 21-character display.text() lines. Same init
// burst + one framebuffer write, so the count matches the short fixture.
[TestFixture]
public class CompatCpFramebufTextLongOnCircuitPythonTests()
    : CircuitPythonOracleBase("compat-cp-framebuf-text-long", expectedTransactions: 43);

[TestFixture]
public class AdafruitSsd1306Unmodified64OnCircuitPythonTests()
    : CircuitPythonOracleBase("adafruit-ssd1306-unmodified-64", expectedTransactions: 50);

[TestFixture]
public class CompatCpLifeOnCircuitPythonTests()
    : CircuitPythonOracleBase("compat-cp-life", expectedTransactions: 99,
        runsForever: true, avrMaxMs: 20_000);
