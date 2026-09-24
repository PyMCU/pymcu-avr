using AVR8Sharp.Core.Peripherals;
using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;
using PyMCU.TestKit;
using System.Security.Cryptography;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Wire-oracle test for the RFC 0009 decision-7 read on the UNMODIFIED Adafruit
/// DHT driver (fixtures/adafruit-dht-optprint -- the same vendored
/// adafruit_dht.py, MD5-pinned, as adafruit-dht-unmodified).
///
/// `dhtDevice.temperature` is a `Union[int, float, None]` property; printing it
/// unnarrowed is the one read that can represent both outcomes, so the tag must
/// pick the member's repr ("23.5", "55.0") or the literal "None" -- the text
/// CPython produces for the same frame. oracle/oracle.py computes that text;
/// the tests record the firmware's serial output on the emulated Uno with a
/// Dht22Simulator on PD2 answering the same frame, and require it.
///
/// The verbatim upstream simpletest (cp-dht test01) is NOT this fixture: its
/// `temperature_f = temperature_c * (9 / 5) + 32` is arithmetic on an
/// unnarrowed Optional, which decision 7 keeps refused -- the site cannot
/// represent the TypeError CPython would raise. This fixture exercises the
/// read the task's option A actually adds.
/// </summary>
[TestFixture]
public class AdafruitDhtOptPrintTests
{
    private const string Fixture = "adafruit-dht-optprint";

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
        var sensor = new Dht22Simulator(uno, uno.PortD, 2);
        sensor.Respond(humidityTenths: 550, temperatureTenths: 235);

        // measure() sleeps 250 ms while the capture runs and main sleeps 200 ms
        // between cycles, so the first line lands well past the usual budget.
        for (int i = 0; i < 15 && !uno.Serial.Text.Contains("t="); i++)
            uno.RunMilliseconds(100);
        TestContext.Out.WriteLine($"Serial: '{uno.Serial.Text}'");
        foreach (var line in _oracle)
            uno.Serial.Text.Should().Contain(line,
                "the unnarrowed Optional print must match what CPython computes for the same frame");
    }

    [Test]
    public void UnnarrowedPropertyPrint_MatchesOracle_CSharpFrontEnd()
        => AssertReadCycle(_session.Reset());

    [Test]
    public void UnnarrowedPropertyPrint_MatchesOracle_PythonFrontEnd()
        => AssertReadCycle(_pySession.Reset());
}
