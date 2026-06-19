using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/zca-slot -- RFC 0001 Model B (SRAM slot). A ZCA with >= 2
/// fields cannot pack into a return register, so it is boxed: its fields live in a fixed SRAM
/// slot and its @outline method takes a `self` pointer, reading each field via BytearrayLoad
/// at a byte offset. Two instances get two distinct 2-byte slots; one shared Sensor_read body
/// walks whichever slot pointer it is handed.
///   a = Sensor(3,4) -> 3*4 = 12
///   b = Sensor(5,7) -> 5*7 = 35
/// Distinct results (12, 35) from one shared body prove per-instance state lives in the slot,
/// not baked into duplicated code.
/// </summary>
[TestFixture]
public class ZcaSlotTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("zca-slot"));

    [Test]
    public void Slot_TwoInstances_DistinctStateOneBody()
    {
        var uno = _session.Reset();
        uno.RunUntilSerialBytes(uno.Serial, 5, maxMs: 400); // "SL\n" (3) + 12 + 35

        var bytes = uno.Serial.Bytes;
        bytes.Length.Should().BeGreaterThanOrEqualTo(5, "banner 'SL\\n' + two results");
        bytes[^2].Should().Be(12, "a = Sensor(3,4); read() = 3*4 = 12");
        bytes[^1].Should().Be(35, "b = Sensor(5,7); read() = 5*7 = 35");
    }
}
