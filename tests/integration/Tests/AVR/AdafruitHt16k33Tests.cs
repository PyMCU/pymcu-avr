using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit;
using PyMCU.TestKit;
using System.Security.Cryptography;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Byte-for-byte I2C oracle test for the UNMODIFIED Adafruit HT16K33 driver's
/// Seg7x4 path (fixtures/adafruit-ht16k33).
///
/// HT16K33 is the demandant for compile-time instance-field constants surviving
/// stacked @inline expansions: `self._buffer`/`self._buffer_size` are bound in
/// HT16K33.__init__ (reached through Seg7x4 -> _AbstractSeg7x4 -> Seg14x4
/// super().__init__ hops) and read inside _put/_adjusted_index/show expansions
/// several inline levels deep. The path also folds class attribute POSITIONS
/// read through `self` via the MRO, `self._chardict` being None into an `and`
/// short-circuit, single-character string iteration feeding char.lower() and
/// ord() through parameter binding, a compile-time `isinstance(display,
/// segments.Seg7x4)` against a module-qualified candidate, and a runtime
/// fixed-size slice `self._buffer[offset : offset + self._buffer_size]`.
///
/// The driver never reads -- after the I2CDevice probe (a zero-length write)
/// every transaction is a command byte or a 17-byte display-buffer dump, so no
/// read script is needed. main.py repeats init + the deterministic half of the
/// segments simpletest in a loop; the time-paced marquee() is left out because
/// its transaction count would depend on wall time vs emulated milliseconds.
/// oracle/oracle.py runs the same files under CPython and prints the
/// transaction stream these tests compare against.
/// </summary>
[TestFixture]
public class AdafruitHt16k33Tests
{
    private const string Fixture = "adafruit-ht16k33";
    private const byte Ht16k33Addr = 0x70;

    // md5 of each vendored file, pinned so an accidental edit fails the test.
    // The library files are the same bytes as the upstream bundle ships.
    private static readonly (string RelPath, string Md5)[] VendoredFiles =
    {
        ("src/adafruit_ht16k33/__init__.py",     "d41d8cd98f00b204e9800998ecf8427e"),
        ("src/adafruit_ht16k33/ht16k33.py",      "4ec95ad319c4e405cce79d9f454c8385"), // Adafruit_CircuitPython_HT16K33 bundle
        ("src/adafruit_ht16k33/matrix.py",       "bcd991bdc81555567bee83bb98c5e9b7"),
        ("src/adafruit_ht16k33/segments.py",     "e9f6d6d1ebac9d9ae0082c3e38519e29"),
        ("src/adafruit_bus_device/__init__.py",  "d41d8cd98f00b204e9800998ecf8427e"),
        ("src/adafruit_bus_device/i2c_device.py","2451d2fdd1ea8bf925976763e7781b4f"),
        ("src/adafruit_bus_device/spi_device.py","d78273b262908517d1425d0a5961dd21"),
        ("src/main.py",                          "19b2b627edfed82cada2e219e784849e"),
    };

    // Construction is 5 transactions once (the I2CDevice probe, init's
    // fill-show, and the oscillator/blink/brightness command writes); each
    // loop iteration is then 13 -- the explicit fill's show plus one show()
    // per print/setitem/property/set_digit_raw call. 31 is init plus two full
    // iterations, so the repeating writes are part of the comparison.
    private const int TxnCount = 31;

    private static string _hex = null!;
    private static string _pyHex = null!;
    private static List<I2cTransaction> _oracle = null!;

    [OneTimeSetUp]
    public void BuildFirmwareAndRunOracle()
    {
        _hex    = PymcuCompiler.BuildFixture(Fixture);
        _pyHex  = PymcuCompiler.BuildFixturePyParser(Fixture);
        _oracle = OracleScript.Run(
            Path.Combine(PymcuCompiler.FixtureDir(Fixture), "oracle", "oracle.py"),
            Path.Combine(PymcuCompiler.Root, ".venv", "bin", "python"));
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
    public void I2cTraffic_MatchesOracle_CSharpFrontEnd()
    {
        var run = UnoTwiTrace.Record(_hex, Ht16k33Addr, TxnCount,
            maxMs: 60000);
        I2cStreams.AssertEqual(_oracle, run, "C# front end");
    }

    [Test]
    public void I2cTraffic_MatchesOracle_PythonFrontEnd()
    {
        var run = UnoTwiTrace.Record(_pyHex, Ht16k33Addr, TxnCount,
            maxMs: 60000);
        I2cStreams.AssertEqual(_oracle, run, "Python front end");
    }

    [Test]
    public void BothFrontEnds_ProduceIdenticalFirmware()
    {
        _pyHex.Should().Be(_hex,
            "both front ends must compile the same sources to byte-identical firmware");
    }
}
