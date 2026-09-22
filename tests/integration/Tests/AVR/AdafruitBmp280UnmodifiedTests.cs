using FluentAssertions;
using NUnit.Framework;
using PyMCU.TestKit;
using System.Security.Cryptography;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Byte-for-byte I2C oracle test for the UNMODIFIED Adafruit BMP280 driver
/// (fixtures/adafruit-bmp280-unmodified) -- the RFC 0009 demandant.
///
/// adafruit_bmp280.py declares `def pressure(self) -&gt; Optional[float]`; the
/// property never reaches a `return None` on this version, so under RFC 0009
/// decision 2 the compiler must keep it tag-free, and the unnarrowed read in
/// `altitude` (`p = self.pressure; p / self.sea_level_pressure`) must keep
/// compiling. The fixture vendors the driver byte-identical to upstream;
/// oracle/oracle.py runs the same files under CPython against a BMP280
/// register file and prints every transaction; these tests record the
/// firmware's TWI traffic on the emulated Uno talking to the same register
/// map and require the two streams to be identical.
///
/// The slave is a TwiRegisterFile, not a plain TwiRecorder: the driver checks
/// the chip-id register (0xD0 == 0x58) and reads the 24-byte calibration block
/// at 0x88, so reads must answer from a real register map.
/// </summary>
[TestFixture]
public class AdafruitBmp280UnmodifiedTests
{
    private const string Fixture = "adafruit-bmp280-unmodified";
    private const byte SensorAddr = 0x77;

    // md5 of each vendored file, pinned so an accidental edit fails the test.
    private static readonly (string RelPath, string Md5)[] VendoredFiles =
    {
        ("src/adafruit_bmp280.py",               "8f55122667f76e6913c52b427ba80565"), // Adafruit_CircuitPython_BMP280
        ("src/adafruit_bus_device/__init__.py",  "d41d8cd98f00b204e9800998ecf8427e"),
        ("src/adafruit_bus_device/i2c_device.py","2451d2fdd1ea8bf925976763e7781b4f"),
        ("src/adafruit_bus_device/spi_device.py","d78273b262908517d1425d0a5961dd21"),
    };

    // The BMP280 register file the oracle's fake bus serves; byte-identical to
    // _bmp280_registers() in oracle.py. Calibration is the datasheet example
    // set (dig_T1=27504 .. dig_P9=6000, "<HhhHhhhhhhhh" at 0x88) so the
    // pressure compensation divisor is non-zero.
    private static byte[] Bmp280Registers()
    {
        var regs = new byte[256];
        byte[] calibration =
        {
            0x70, 0x6B, 0x43, 0x67, 0x18, 0xFC, 0x7D, 0x8E,
            0x43, 0xD6, 0xD0, 0x0B, 0x27, 0x0B, 0x8C, 0x00,
            0xF9, 0xFF, 0x8C, 0x3C, 0xF8, 0xC6, 0x70, 0x17,
        };
        Array.Copy(calibration, 0, regs, 0x88, calibration.Length);
        regs[0xD0] = 0x58;                 // _CHIP_ID
        regs[0xF3] = 0x00;                 // status: measuring=0, im_update=0
        regs[0xF7] = 0x65; regs[0xF8] = 0x19; regs[0xF9] = 0x00;  // press raw
        regs[0xFA] = 0x7E; regs[0xFB] = 0x40; regs[0xFC] = 0x00;  // temp raw
        return regs;
    }

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
        var run = UnoTwiTrace.Record(_hex,
            twi => new TwiRegisterFile(twi, SensorAddr, Bmp280Registers()), _oracle.Count);
        I2cStreams.AssertEqual(_oracle, run, "C# front end");
    }

    [Test]
    public void I2cTraffic_MatchesOracle_PythonFrontEnd()
    {
        var run = UnoTwiTrace.Record(_pyHex,
            twi => new TwiRegisterFile(twi, SensorAddr, Bmp280Registers()), _oracle.Count);
        I2cStreams.AssertEqual(_oracle, run, "Python front end");
    }

    [Test]
    public void BothFrontEnds_ProduceIdenticalFirmware()
    {
        _pyHex.Should().Be(_hex,
            "both front ends must compile the same sources to byte-identical firmware");
    }
}
