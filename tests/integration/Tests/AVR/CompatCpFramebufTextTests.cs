// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using PyMCU.TestKit;
using System.Security.Cryptography;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// RFC 0008 demandant (docs/rfcs/0008-embedded-files.md, Section 7): the
/// unmodified Adafruit SSD1306 driver renders <c>display.text("PyMCU", 0, 0, 1)</c>
/// -- which reaches <c>adafruit_framebuf.BitmapFont</c> and its
/// <c>open("font5x8.bin", "rb")</c> -- then <c>display.show()</c> writes the
/// 128x32 MONO_VLSB framebuffer over I2C. The font is an embedded romfs blob
/// (1282 bytes): open() resolves at compile time, and every seek/read lands on
/// flash through the same machinery const tables use.
///
/// Same oracle shape as <see cref="AdafruitSsd1306UnmodifiedTests"/>: the CPython
/// side (oracle/oracle.py, a copy of that fixture's oracle) runs the identical
/// vendored sources plus the identical font file against a fake I2C bus and
/// prints every transaction; the test records the emulated Uno's TWI traffic and
/// requires the two streams to be byte-identical. The last transaction is the
/// show(): control byte 0x40 followed by 512 framebuffer bytes whose first 30
/// columns carry the glyphs -- P=127,9,9,9,6 y=76,144,144,144,124
/// M=127,2,28,2,127 C=62,65,65,65,34 U=63,64,64,64,63, one blank column each.
/// </summary>
[TestFixture]
public class CompatCpFramebufTextTests
{
    private const string Fixture = "compat-cp-framebuf-text";
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
