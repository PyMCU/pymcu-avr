// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using PyMCU.TestKit;
using System.Security.Cryptography;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Long-string twin of <see cref="CompatCpFramebufTextTests"/>: the same
/// unmodified Adafruit SSD1306 + framebuf pair renders
/// <c>display.text("PyMCU hello world", 0, 0, 1)</c> (17 chars) and
/// <c>display.text("AVR says hello world!", 0, 10, 1)</c> (21 chars).
///
/// text() iterates each line with `for i, char in enumerate(chunk)`, and a
/// compile-time string past the 8-char unroll cap used to be refused at
/// compile time ("unrolls it at compile time ... Loop over range(len(s))").
/// The strings are now interned in flash and iterated at run time -- a counter
/// and an LPM read per character, exactly what `for c in s` already did past
/// its own cap. `char` is a run-time uint8 char code; ord(char) is the
/// identity on it, and draw_char's font seek takes the run-time offset.
///
/// The oracle is the same shape as the short-string fixture: CPython runs the
/// identical vendored sources and font against a fake I2C bus; the test
/// requires the emulated Uno's TWI traffic to be byte-identical.
/// </summary>
[TestFixture]
public class CompatCpFramebufTextLongTests
{
    private const string Fixture = "compat-cp-framebuf-text-long";
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
    public void Text_Show_SendsTheSameI2cTrafficAsCPython()
    {
        var run = UnoTwiTrace.Record(_hex, OledAddr, _oracle.Count);
        I2cStreams.AssertEqual(_oracle, run, "C# front end");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
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
