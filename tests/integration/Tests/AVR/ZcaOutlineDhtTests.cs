using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/zca-outline-dht -- RFC 0001 Model A applied to a
/// DHT-style driver written the NATURAL way (the full bit-bang protocol lives in
/// DHT.read(self), keyed on the runtime field self.pin, with no hand-rolled
/// "thin dispatch + shared worker" split).
///
/// With @outline the compiler emits ONE DHT_read body shared by all three sensors
/// (pins 2, 3, 4); each instance calls it with its own pin. Flipping to @inline
/// duplicates the entire protocol three times (~3596 B vs ~1268 B here). This test
/// pins the shared-body behaviour: no sensor is attached, so every read() times out
/// on the ACK wait and returns 0xFFFF -> three 0xFF bytes after the "DHT" banner.
/// </summary>
[TestFixture]
public class ZcaOutlineDhtTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("zca-outline-dht"));

    [Test]
    public void Outline_ThreeSensors_ShareOneReadBody()
    {
        var uno = _session.Reset();
        // Banner "DHT\n" (4 bytes) then three error bytes (0xFF) -- one per sensor,
        // all produced by the single shared DHT_read body.
        uno.RunUntilSerialBytes(uno.Serial, 7, maxMs: 4000);

        var bytes = uno.Serial.Bytes;
        bytes.Length.Should().BeGreaterThanOrEqualTo(7, "banner 'DHT\\n' (4) + three result bytes");
        // The last three bytes are the low byte of each read() == 0xFF (no sensor).
        bytes[^1].Should().Be(0xFF, "sensor c read() timed out -> 0xFFFF");
        bytes[^2].Should().Be(0xFF, "sensor b read() timed out -> 0xFFFF");
        bytes[^3].Should().Be(0xFF, "sensor a read() timed out -> 0xFFFF");
    }
}
