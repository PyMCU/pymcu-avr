using AVR8Sharp.Core.Peripherals;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;
using PyMCU.TestKit;
using System.Security.Cryptography;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Wire-oracle test for the UNMODIFIED Adafruit DHT driver
/// (fixtures/adafruit-dht-unmodified) -- the RFC 0009 phase-3 demandant.
///
/// adafruit_dht.py declares `def temperature(self) -> Union[int, float, None]`
/// (and `humidity` the same); the fields `_temperature`/`_humidity` are written
/// `None` in `DHTBase.__init__` and a float in `measure()`, so the fixture
/// exercises tagged union fields across a class hierarchy plus
/// `isinstance(t, float)` tag narrowing in main.py. oracle/oracle.py computes
/// under stock CPython the exact UART line that frame decodes to; the tests
/// record the firmware's serial output on the emulated Uno with a
/// Dht22Simulator on PD2 answering the same frame, and require the line the
/// oracle printed.
/// </summary>
[TestFixture]
public class AdafruitDhtUnmodifiedTests
{
    private const string Fixture = "adafruit-dht-unmodified";

    // md5 of each vendored file, pinned so an accidental edit fails the test.
    private static readonly (string RelPath, string Md5)[] VendoredFiles =
    {
        ("src/adafruit_dht.py", "e954871c131ad4d9b42ed383f449ce88"), // Adafruit_CircuitPython_DHT
    };

    private static SimSession _session = null!;
    private static SimSession _pySession = null!;
    private static List<string> _oracle = null!;

    [OneTimeSetUp]
    public void BuildFirmwareAndRunOracle()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture(Fixture));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser(Fixture));
        _oracle = OracleScript.RunLines(
            Path.Combine(PymcuCompiler.FixtureDir(Fixture), "oracle", "oracle.py"),
            Path.Combine(PymcuCompiler.Root, ".venv", "bin", "python"));
    }

    // ── Vendored-file pin ───────────────────────────────────────────────────

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

    // ── Wire-oracle runs ────────────────────────────────────────────────────

    private static void AssertReadCycle(ArduinoUnoSimulation uno)
    {
        // The frame the oracle computed: humidity 550 tenths, temperature 235.
        // Respond() parks the sim until the firmware's resume(trigger) releases
        // the line to input+pull-up, then streams the ACK and the 40 data bits.
        var sensor = new Dht22Simulator(uno, uno.PortD, 2);
        sensor.Respond(humidityTenths: 550, temperatureTenths: 235);

        // measure() sleeps 250 ms while the capture runs and main sleeps 200 ms
        // between cycles, so the first line lands well past the usual budget.
        for (int i = 0; i < 15 && !uno.Serial.Text.Contains("Temp:"); i++)
            uno.RunMilliseconds(100);
        TestContext.Out.WriteLine($"Serial: '{uno.Serial.Text}'");
        foreach (var line in _oracle)
            uno.Serial.Text.Should().Contain(line,
                "the firmware's print must match what CPython computes for the same frame");
    }

    [Test]
    public void Reading_MatchesOracle_CSharpFrontEnd()
        => AssertReadCycle(_session.Reset());

    [Test]
    public void Reading_MatchesOracle_PythonFrontEnd()
        => AssertReadCycle(_pySession.Reset());
}
