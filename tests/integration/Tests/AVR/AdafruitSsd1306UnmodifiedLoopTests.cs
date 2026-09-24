// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using PyMCU.TestKit;
using System.Security.Cryptography;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Loop twin of <see cref="AdafruitSsd1306UnmodifiedTests"/>: the upstream
/// ssd1306_simpletest body -- <c>fill</c>, three <c>pixel()</c> calls, two
/// <c>show()</c>s -- inside <c>while True:</c>, the shape flashed firmware
/// actually has. Every receiver method call in a loop walks the fields the
/// method writes; <c>main.py</c> here is the loop harness (not vendored, so it
/// carries no md5 pin), while the Adafruit pair stays byte-identical upstream.
/// The program parks in <c>while True: pass</c> after three iterations; the
/// oracle's FakeI2C raises _Done at the same framebuffer count.
/// </summary>
[TestFixture]
public class AdafruitSsd1306UnmodifiedLoopTests
{
    private const string Fixture = "adafruit-ssd1306-unmodified-loop";
    private const byte OledAddr = 0x3C;

    // md5 of each vendored file, pinned so an accidental edit fails the test.
    private static readonly (string RelPath, string Md5)[] VendoredFiles =
    {
        ("src/adafruit_ssd1306.py",              "d70a9d7eb565ebf6f4ff595ee9076956"), // Adafruit_CircuitPython_SSD1306 tag 2.12.24
        ("src/adafruit_framebuf.py",             "f8a793f779537106702c42cb8569dcd1"), // PyPI adafruit-circuitpython-framebuf 1.6.12
        ("src/adafruit_bus_device/__init__.py",  "d41d8cd98f00b204e9800998ecf8427e"),
        ("src/adafruit_bus_device/i2c_device.py","2451d2fdd1ea8bf925976763e7781b4f"),
        ("src/adafruit_bus_device/spi_device.py","d78273b262908517d1425d0a5961dd21"),
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
    public void I2cTraffic_InALoop_MatchesOracle_CSharpFrontEnd()
    {
        var run = UnoTwiTrace.Record(_hex, OledAddr, _oracle.Count);
        I2cStreams.AssertEqual(_oracle, run, "C# front end");
    }

    [Test]
    public void I2cTraffic_InALoop_MatchesOracle_PythonFrontEnd()
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
