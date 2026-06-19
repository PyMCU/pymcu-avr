using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/zca-array -- RFC 0001 Model B (Class[N]). An array of boxed
/// ZCA instances: N sensors laid out contiguously in SRAM (one 2-byte slot each), all driven by
/// ONE shared Sensor_read through a RUNTIME index. sensors[i] is base + i*stride; sensors[i].read()
/// passes that element address as the self pointer. This is the "multiple DHT" case -- N
/// instances, one method body, a loop.
///   sensors[0]=Sensor(3,4)->12 ; sensors[1]=Sensor(5,7)->35 ; sensors[2]=Sensor(2,9)->18
/// </summary>
[TestFixture]
public class ZcaArrayTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("zca-array"));

    [Test]
    public void InstanceArray_RuntimeIndex_OneSharedBody()
    {
        var uno = _session.Reset();
        uno.RunUntilSerialBytes(uno.Serial, 6, maxMs: 500); // "AR\n" (3) + 12 + 35 + 18

        var bytes = uno.Serial.Bytes;
        bytes.Length.Should().BeGreaterThanOrEqualTo(6, "banner 'AR\\n' + three results");
        bytes[^3].Should().Be(12, "sensors[0] = Sensor(3,4); read() = 12");
        bytes[^2].Should().Be(35, "sensors[1] = Sensor(5,7); read() = 35");
        bytes[^1].Should().Be(18, "sensors[2] = Sensor(2,9); read() = 18 (runtime index, shared body)");
    }
}
