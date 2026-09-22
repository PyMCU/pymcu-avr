using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit;
using PyMCU.TestKit;
using System.Security.Cryptography;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Byte-for-byte I2C oracle test for the UNMODIFIED Adafruit Seesaw driver's
/// analog_read path (fixtures/adafruit-seesaw-analog).
///
/// The fixture vendors the same byte-identical adafruit_seesaw + bus_device
/// sources as the simpletest fixture, but main.py calls
/// `ss.analog_read(2)` -- the demandant for class-object fields: __init__ binds
/// `self.pin_mapping` to one of five pinmap CLASSES under an elif chain on wire
/// data (pid/chip id), and analog_read reads the class-level tuple
/// `self.pin_mapping.analog_pins` twice -- `pin not in ...` and
/// `....index(pin)`. The compiler stores the field as a tag byte and dispatches
/// the attribute reads per candidate class.
///
/// The scripted slave answers 0x55 (SAMD09), so pin_mapping is SAMD09_Pinmap:
/// pin 2 is in its analog_pins (2,3,4,5,6,7), index() answers 0, and the read
/// writes [0x09, 0x07] then reads two bytes (the oracle's 0xFF tail).
/// oracle/oracle.py runs the same files under CPython and prints the
/// transaction stream these tests compare against.
/// </summary>
[TestFixture]
public class AdafruitSeesawAnalogTests
{
    private const string Fixture = "adafruit-seesaw-analog";
    private const byte SeesawAddr = 0x49;

    // md5 of each vendored file, pinned so an accidental edit fails the test.
    // The library files are the same bytes as the simpletest fixture vendors.
    private static readonly (string RelPath, string Md5)[] VendoredFiles =
    {
        ("src/adafruit_seesaw/__init__.py",      "d41d8cd98f00b204e9800998ecf8427e"),
        ("src/adafruit_seesaw/seesaw.py",        "55ab393a1fe6b3361bfc849b2e5f074a"), // Adafruit_CircuitPython_seesaw bundle
        ("src/adafruit_seesaw/attiny8x7.py",     "8f45756b607df947fafab2167f0c4e9a"),
        ("src/adafruit_seesaw/attinyx16.py",     "2fc3a00064b52e409cc65feea71b01bf"),
        ("src/adafruit_seesaw/crickit.py",       "3697f8795be155a64b347264f1339a65"),
        ("src/adafruit_seesaw/robohat.py",       "3b177944c3abbd137605f0a017697ee8"),
        ("src/adafruit_seesaw/samd09.py",        "fd3bb6e08a736584bf9e95eb23ed812d"),
        ("src/adafruit_bus_device/__init__.py",  "d41d8cd98f00b204e9800998ecf8427e"),
        ("src/adafruit_bus_device/i2c_device.py","2451d2fdd1ea8bf925976763e7781b4f"),
        ("src/adafruit_bus_device/spi_device.py","d78273b262908517d1425d0a5961dd21"),
        ("src/main.py",                          "25b1a36a106cc339f0160083ac081e01"),
    };

    // The slave's read answers, in wire order, one group per loop iteration:
    // the 1-byte HW_ID read gets 0x55 (SAMD09 -- a valid chip id, so the
    // constructor does not raise), the 4-byte VERSION read gets 0x00000001
    // (pid 0, matching no named product, so pin_mapping lands on
    // SAMD09_Pinmap), and the 2-byte ADC read gets 0xFFFF -- the same answers
    // the oracle's fake bus hands CPython (which replies by read length, so it
    // must be spelled per iteration here).
    private static readonly byte[] ReadScript =
        { 0x55, 0x00, 0x00, 0x00, 0x01, 0xFF, 0xFF,
          0x55, 0x00, 0x00, 0x00, 0x01, 0xFF, 0xFF };

    // One loop iteration is init's 6 transactions (probe, sw_reset, hw-id
    // write+read, version write+read) plus analog_read's write+read pair. 16 is
    // two full iterations, so the repeating read is part of the comparison.
    private const int TxnCount = 16;

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
        var run = UnoTwiTrace.Record(_hex, SeesawAddr, TxnCount,
            maxMs: 60000, readScript: ReadScript);
        I2cStreams.AssertEqual(_oracle, run, "C# front end");
    }

    [Test]
    public void I2cTraffic_MatchesOracle_PythonFrontEnd()
    {
        var run = UnoTwiTrace.Record(_pyHex, SeesawAddr, TxnCount,
            maxMs: 60000, readScript: ReadScript);
        I2cStreams.AssertEqual(_oracle, run, "Python front end");
    }

    [Test]
    public void BothFrontEnds_ProduceIdenticalFirmware()
    {
        _pyHex.Should().Be(_hex,
            "both front ends must compile the same sources to byte-identical firmware");
    }
}
