using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;
using PyMCU.TestKit;
using System.Security.Cryptography;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Wire-oracle test for the UNMODIFIED Adafruit DS18x20 and OneWire drivers
/// (fixtures/adafruit-ds18x20-unmodified), with the library's simpletest verbatim as main.py.
///
/// The firmware scans the bus (SEARCH ROM, 64 bit steps), builds an OneWireAddress from the ROM it
/// found, asks the sensor to convert and reads the scratchpad back, so it goes through every
/// feature the example needs from the language: a buffer that is None on the first scan step and a
/// buffer after, a tuple return with an optional buffer slot, a growable list of instances and the
/// address read back out of it. A OneWireSlaveSimulator on PD5 (board.D5) answers as a DS18B20
/// holding 23.5 C, and oracle/oracle.py prints under stock CPython the line that reading decodes to.
/// </summary>
[TestFixture]
public class AdafruitDs18x20UnmodifiedTests
{
    private const string Fixture = "adafruit-ds18x20-unmodified";

    // md5 of each vendored file, pinned so an accidental edit fails the test.
    private static readonly (string RelPath, string Md5)[] VendoredFiles =
    {
        ("src/adafruit_ds18x20.py", "69428f19a252deb15cff1ff56ac8d474"),
        ("src/adafruit_onewire/bus.py", "4ad3c651872ab744d8ffb3064785f32c"),
        ("src/adafruit_onewire/device.py", "0092c11d7837994454c35eae9c9375ed"),
    };

    private static readonly byte[] Serial = { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66 };

    private static SimSession _session = null!;
    private static List<string> _oracle = null!;

    [OneTimeSetUp]
    public void BuildFirmwareAndRunOracle()
    {
        _session = new SimSession(PymcuCompiler.BuildFixture(Fixture));
        _oracle = OracleScript.RunLines(
            Path.Combine(PymcuCompiler.FixtureDir(Fixture), "oracle", "oracle.py"),
            Path.Combine(PymcuCompiler.Root, ".venv", "bin", "python"));
    }

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
    public void TheTemperatureTheSensorHoldsIsPrinted()
    {
        var uno = _session.Reset();
        var slave = new OneWireSlaveSimulator(uno, uno.PortD, 5, 0x2A, 0x2B, Serial, 0x0178);

        slave.RunUntil(() => uno.Serial.Text.Contains("Temperature:") && uno.Serial.Text.Contains("C\n"), 4000)
            .Should().BeTrue($"the firmware prints a reading; serial so far: '{uno.Serial.Text}'");
        TestContext.Out.WriteLine($"Serial: '{uno.Serial.Text}'");
        foreach (var line in _oracle)
            uno.Serial.Text.Should().Contain(line,
                "the firmware's print must match what CPython computes for the same scratchpad");
    }

    [Test]
    public void TheMasterAddressesTheSensorByTheRomItFound()
    {
        var uno = _session.Reset();
        var slave = new OneWireSlaveSimulator(uno, uno.PortD, 5, 0x2A, 0x2B, Serial, 0x0178);

        slave.RunUntil(() => uno.Serial.Text.Contains("Temperature:"), 4000).Should().BeTrue();

        // The scan used SEARCH ROM, and every later transaction selected the device with MATCH ROM
        // carrying the eight bytes the scan returned: a ROM copied wrong would fail the match and
        // the sensor would never answer.
        slave.CommandsSeen.Should().StartWith(new byte[] { 0xF0, 0x55 });
        slave.CommandsSeen.Should().Contain(new byte[] { 0x44, 0xBE });
    }
}
