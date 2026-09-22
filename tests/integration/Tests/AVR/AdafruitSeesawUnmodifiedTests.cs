using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;
using AVR8Sharp.Core.Peripherals;
using PyMCU.TestKit;
using System.Security.Cryptography;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Byte-for-byte I2C oracle test for the UNMODIFIED Adafruit Seesaw driver
/// (fixtures/adafruit-seesaw-unmodified).
///
/// The fixture vendors adafruit_seesaw/seesaw.py plus its five pinmap modules,
/// adafruit_bus_device byte-identical to upstream, and the upstream
/// seesaw_simpletest.py as main.py. oracle/oracle.py runs the same files under
/// CPython against a fake I2C bus and prints every transaction it emits; these
/// tests record the firmware's TWI traffic on the emulated Uno and require the
/// two streams to be identical. Any difference is a compiler bug, not a test
/// adjustment: the oracle is the reference.
///
/// The simpletest path is also the deferred raise-message program's neighbour:
/// __init__ reads the HW_ID byte and would raise
/// `RuntimeError(f"Seesaw hardware ID returned 0x{self.chip_id:x} is not "
/// "correct! ...")` on a wrong answer -- the mixed f-string/literal spelling
/// the parser now merges. The scripted slave answers 0x55 (SAMD09), so the
/// constructor continues to get_version, the pinmap selection, and the
/// pin_mode/digital_write loop whose writes go through `cmd[offset:] =
/// struct.pack(">I", pins)` and `cmd = struct.pack(">I", pins)`.
/// </summary>
[TestFixture]
public class AdafruitSeesawUnmodifiedTests
{
    private const string Fixture = "adafruit-seesaw-unmodified";
    private const byte SeesawAddr = 0x49;

    // md5 of each vendored file, pinned so an accidental edit fails the test.
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
        ("src/main.py",                          "419944a304b2f565add0355e77deea0f"), // seesaw_simpletest.py verbatim
    };

    // The slave's read answers, in wire order: the 1-byte HW_ID read gets 0x55
    // (SAMD09 -- a valid chip id, so the constructor does not raise), and the
    // 4-byte VERSION read gets 0x00000001 (pid 0, matching no named product, so
    // pin_mapping lands on SAMD09_Pinmap). The same bytes the oracle's fake bus
    // hands CPython.
    private static readonly byte[] ReadScript = { 0x55, 0x00, 0x00, 0x00, 0x01 };

    // Init is 6 transactions (probe, sw_reset, hw-id write+read, version
    // write+read), pin_mode one more, and each blink iteration writes twice.
    // 15 transactions is four full LED toggles past setup.
    private const int TxnCount = 15;

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

    // The scripted chip id is 0x55 = SAMD09, so __init__ never raises. When the
    // read answers an id outside the known set the constructor's raise fires --
    // `RuntimeError(f"Seesaw hardware ID returned 0x{self.chip_id:x} is not "
    // "correct! ...")` -- and the deferred-print machinery must write the same
    // UART text CPython's traceback line carries. The all-0xFF slave drives that
    // path: chip id 0xFF is in no set, so the raise runs on the very first read.
    [Test]
    public void WrongChipId_RaisesTheDeferredFStringMessage()
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(_hex);
        uno.PortC.SetPinValue(4, true);   // SDA
        uno.PortC.SetPinValue(5, true);   // SCL
        uno.AddTwi(AvrTwi.TwiConfig, out var twi);
        var recorder = new TwiRecorder(twi, SeesawAddr);   // reads answer 0xFF
        twi.EventHandler = recorder;

        // Unhandled exceptions print `E:<Type>: <message>` and halt. Wait for the
        // message's tail: stopping at an earlier substring would cut the serial
        // text mid-write and lose the merged literal's second half.
        uno.RunUntilSerial(uno.Serial, "Please check your wiring.", maxMs: 20000);
        uno.Serial.Text.Should().Contain(
            "E:RuntimeError: Seesaw hardware ID returned 0xff is not correct! "
            + "Please check your wiring.",
            "the raised RuntimeError must carry the f-string's formatted chip id, "
            + "exactly as print(f\"...0x{chip:x}...\") would write it");
    }
}
