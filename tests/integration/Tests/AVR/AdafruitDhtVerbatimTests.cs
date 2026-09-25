using AVR8Sharp.Core.Peripherals;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;
using PyMCU.TestKit;
using System.Security.Cryptography;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The RFC 0009 decision-7 contract, pinned on the wire: the VERBATIM upstream
/// dht_simpletest.py (fixtures/adafruit-dht-verbatim -- same source as the
/// cp-dht demandant modulo the pin: upstream hardcodes board.D18, which does
/// not exist on the Uno; the fixture uses board.D2, the wire the testbench's
/// Dht22Simulator sits on) plus the companion fixture that reaches the path
/// the verbatim loop cannot (fixtures/adafruit-dht-none-cached).
///
/// The verbatim program does `temperature_c = dhtDevice.temperature` -- a
/// property declared `Union[int, float, None]` over fields written `None` in
/// `DHTBase.__init__` -- and then `temperature_c * (9 / 5) + 32` with no
/// narrowing. That is the decision-7 demandant: the unnarrowed arithmetic
/// lowers to a runtime tag dispatch, and on a float member it prints exactly
/// what CPython prints for the same frame.
///
/// The None path is the second half of the contract. It is NOT reachable in
/// the verbatim loop, and it is worth saying why, because it is subtle:
/// measure() stamps _last_called before the wire read and only fills
/// _temperature/_humidity on success, so `temperature` yields None only when a
/// later read lands inside the driver's 2 s cache window with the fields still
/// None. The verbatim retry always misses it -- the failed read's own 0.25 s
/// capture plus the handler's time.sleep(2.0) put the next call ~2.27 s out,
/// so on a dead sensor the verbatim program prints the caught RuntimeError
/// text forever, exactly as it does on real CircuitPython hardware.
/// adafruit-dht-none-cached keeps the same arithmetic and the same
/// try/except RuntimeError/except Exception chain but drops the 2 s sleep so
/// the second read lands inside the window: the property returns None, the
/// multiplication raises TypeError, `except Exception` re-raises it through
/// dhtDevice.exit(), and the unhandled report carries CPython's exact wording.
/// </summary>
[TestFixture]
public class AdafruitDhtVerbatimTests
{
    private const string Fixture = "adafruit-dht-verbatim";
    private const string NoneFixture = "adafruit-dht-none-cached";

    // md5 of each vendored/demandant file, pinned so an accidental edit fails
    // the test.
    private static readonly (string FixtureName, string RelPath, string Md5)[] VendoredFiles =
    {
        (Fixture,     "src/adafruit_dht.py", "e954871c131ad4d9b42ed383f449ce88"), // Adafruit_CircuitPython_DHT
        (Fixture,     "src/main.py",         "cb65816051c6582e69cde3a48fe013df"), // dht_simpletest.py verbatim, pin D18->D2
        (NoneFixture, "src/adafruit_dht.py", "e954871c131ad4d9b42ed383f449ce88"), // same vendored driver
    };

    private static SimSession _session = null!;
    private static SimSession _pySession = null!;
    private static SimSession _noneSession = null!;
    private static SimSession _nonePySession = null!;
    private static List<string> _oracle = null!;

    [OneTimeSetUp]
    public void BuildFirmwareAndRunOracle()
    {
        _session       = new SimSession(PymcuCompiler.BuildFixture(Fixture));
        _pySession     = new SimSession(PymcuCompiler.BuildFixturePyParser(Fixture));
        _noneSession   = new SimSession(PymcuCompiler.BuildFixture(NoneFixture));
        _nonePySession = new SimSession(PymcuCompiler.BuildFixturePyParser(NoneFixture));
        _oracle = OracleScript.RunLines(
            Path.Combine(PymcuCompiler.FixtureDir(Fixture), "oracle", "oracle.py"),
            Path.Combine(PymcuCompiler.Root, ".venv", "bin", "python"));
    }

    // ── Vendored-file pin ───────────────────────────────────────────────────

    [Test]
    public void VendoredFiles_AreByteIdenticalToUpstream()
    {
        foreach (var (fixtureName, rel, expectedMd5) in VendoredFiles)
        {
            var path = Path.Combine(
                PymcuCompiler.FixtureDir(fixtureName), rel.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(path).Should().BeTrue($"vendored file {fixtureName}/{rel} must exist in the fixture");
            var actual = Convert.ToHexString(MD5.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
            actual.Should().Be(expectedMd5,
                $"{fixtureName}/{rel} must stay byte-identical to upstream, or the oracle no longer measures the verbatim program");
        }
    }

    // ── Valid frame: the verbatim simpletest prints what CPython prints ─────

    private static void AssertReadCycle(ArduinoUnoSimulation uno)
    {
        // The frame the oracle computed: humidity 550 tenths, temperature 235.
        // temperature and humidity are separate wire reads while _last_called
        // is still 0 (the == 0 fast path stays open until a measure() entry
        // lands past the first Timer0 tick), so frames are fed until the Temp
        // line lands. A read starts with the firmware holding the pin LOW for
        // ~1 ms (the trigger) before releasing to input+pullup for the 250 ms
        // capture; polling at 0.2 ms cannot slip past the trigger, and
        // Respond() then parks on the release edge and streams the ACK + 40
        // data bits into the live capture.
        var sensor = new Dht22Simulator(uno, uno.PortD, 2);
        for (int i = 0; i < 12 && !uno.Serial.Text.Contains("Temp:"); i++)
        {
            for (int j = 0; j < 20000 && uno.PortD.GetPinState(2) == PinState.InputPullup; j++)
                uno.RunMilliseconds(0.2);
            sensor.Respond(humidityTenths: 550, temperatureTenths: 235);
            uno.RunMilliseconds(300);
        }
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

    // ── Dead sensor: the verbatim loop catches RuntimeError and retries ──────
    //
    // This is the failure mode the verbatim program actually has on the wire:
    // every property read does a real read, the 0 pulses decode as "DHT sensor
    // not found", the except RuntimeError arm prints it and loops -- the
    // TypeError path is unreachable here, identically to CPython.

    private static void AssertDeadSensorRetries(ArduinoUnoSimulation uno)
    {
        for (int i = 0; i < 60; i++)
            uno.RunMilliseconds(100);
        var text = uno.Serial.Text;
        TestContext.Out.WriteLine($"Serial: '{text}'");
        text.Split("DHT sensor not found, check wiring").Length.Should().BeGreaterThan(2,
            "the verbatim loop must catch the RuntimeError and retry, not halt");
        text.Should().NotContain("Temp:", "no read succeeded -- the sensor never answered");
        text.Should().NotContain("TypeError",
            "the verbatim loop cannot reach the None arithmetic: the retry lands outside the 2 s cache window");
    }

    [Test]
    public void DeadSensor_PrintsCaughtRuntimeError_CSharpFrontEnd()
        => AssertDeadSensorRetries(_session.Reset());

    [Test]
    public void DeadSensor_PrintsCaughtRuntimeError_PythonFrontEnd()
        => AssertDeadSensorRetries(_pySession.Reset());

    // ── None path: a cached read yields None and the arithmetic faults ──────

    private static void AssertNoneArithmeticTypeError(ArduinoUnoSimulation uno)
    {
        // The simulator never answers: the first read raises RuntimeError (the
        // program catches it), the second lands inside the 2 s cache window and
        // returns _temperature -- still None -- so `temperature_c * (9 / 5)`
        // raises TypeError, re-raised through `except Exception` /
        // dhtDevice.exit() / `raise error` to the unhandled report.
        for (int i = 0; i < 40 && !uno.Serial.Text.Contains("E:"); i++)
            uno.RunMilliseconds(100);
        TestContext.Out.WriteLine($"Serial: '{uno.Serial.Text}'");
        uno.Serial.Text.Should().Contain(
            "E:TypeError: unsupported operand type(s) for *: 'NoneType' and 'float'",
            "arithmetic on a None read must raise CPython's exact TypeError, uncaught by except RuntimeError");
    }

    [Test]
    public void NoneRead_ArithmeticRaisesTypeError_CSharpFrontEnd()
        => AssertNoneArithmeticTypeError(_noneSession.Reset());

    [Test]
    public void NoneRead_ArithmeticRaisesTypeError_PythonFrontEnd()
        => AssertNoneArithmeticTypeError(_nonePySession.Reset());
}
