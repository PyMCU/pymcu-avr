using FluentAssertions;
using NUnit.Framework;
using PyMCU.TestKit;
using System.Security.Cryptography;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Byte-for-byte I2C oracle test for the UNMODIFIED Adafruit SSD1306 driver on
/// the 128x64 geometry (fixtures/adafruit-ssd1306-unmodified-64) -- the most
/// common module shape, where <see cref="AdafruitSsd1306UnmodifiedTests"/> is
/// the 128x32 sibling.
///
/// The fixture vendors the same byte-identical upstream library files, and its
/// main.py is the upstream ssd1306_simpletest.py with exactly two differences:
/// display_height = 64 (the only upstream line that changes) and one added
/// display.text("PyMCU", 0, 0, 1) before the final show(). One program then
/// covers init on 64 rows (multiplex 0x3F, COM pins 0x12), fill, pixels and
/// text -- every show() pushes the 1024-byte MONO_VLSB framebuffer as a single
/// 1025-byte transaction (control byte 0x40 + 1024), twice the -32 payload.
///
/// Same oracle shape as the -32 class: oracle/oracle.py runs the vendored
/// sources under CPython against a fake I2C bus and prints every transaction
/// (50 here: probe + poweron + 27 init commands + 3 show() bursts of
/// 6 commands + 1 data write each); the tests record the emulated Uno's TWI
/// traffic and require the streams identical. font5x8.bin is pinned too --
/// text() reaches BitmapFont's open(), which the compiler resolves to the
/// embedded romfs blob.
/// </summary>
[TestFixture]
public class AdafruitSsd1306Unmodified64Tests
{
    private const string Fixture = "adafruit-ssd1306-unmodified-64";
    private const byte OledAddr = 0x3C;

    // md5 of each vendored file, pinned so an accidental edit fails the test.
    private static readonly (string RelPath, string Md5)[] VendoredFiles =
    {
        ("src/adafruit_ssd1306.py",              "d70a9d7eb565ebf6f4ff595ee9076956"), // Adafruit_CircuitPython_SSD1306 tag 2.12.24
        ("src/adafruit_framebuf.py",             "f8a793f779537106702c42cb8569dcd1"), // PyPI adafruit-circuitpython-framebuf 1.6.12
        ("src/adafruit_bus_device/__init__.py",  "d41d8cd98f00b204e9800998ecf8427e"),
        ("src/adafruit_bus_device/i2c_device.py","2451d2fdd1ea8bf925976763e7781b4f"),
        ("src/adafruit_bus_device/spi_device.py","d78273b262908517d1425d0a5961dd21"),
        ("src/font5x8.bin",                      "221fa943a9d845f68ab2155c75d644fe"), // Adafruit_CircuitPython_framebuf font5x8.bin
        ("src/main.py",                          "82f9727d9e22cf55b1a64d6fabc53a3a"), // simpletest + height=64 + text line
    };

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
        var run = UnoTwiTrace.Record(_hex, OledAddr, _oracle.Count);
        I2cStreams.AssertEqual(_oracle, run, "C# front end");
    }

    [Test]
    public void I2cTraffic_MatchesOracle_PythonFrontEnd()
    {
        var run = UnoTwiTrace.Record(_pyHex, OledAddr, _oracle.Count);
        I2cStreams.AssertEqual(_oracle, run, "Python front end");
    }

    [Test]
    public void BothFrontEnds_ProduceIdenticalFirmware()
    {
        _pyHex.Should().Be(_hex,
            "both front ends must compile the same sources to byte-identical firmware");
    }
}
