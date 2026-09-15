using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for fixtures/zca-factory-unannotated-field -- PyMCU#429. A single-field
/// factory class whose field is a bare, UNANNOTATED constructor-parameter passthrough
/// (`self.base = base`) used to read back as zero at every call site: DeriveFieldLayout gave
/// the field an empty type string, which failed IsOutlineSafe's scalar check and left
/// read()'s call site on the ordinary Model A flattened `<instance>_<field>` convention -- a
/// name the RFC 0001 Model B factory-handle assignment never writes.
///   make_sensor(9).read() = 9 + 1 = 10
///   make_sensor(24).read() = 24 + 1 = 25
/// Before the fix this printed "1" and "1" (0 + 1 twice, the never-written flattened field).
/// </summary>
[TestFixture]
public class ZcaFactoryUnannotatedFieldTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("zca-factory-unannotated-field"));

    [Test]
    public void UnannotatedFieldFactoryHandle_ThreadsTheRealValue_NotZero()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("25"), maxMs: 400);
        uno.Serial.Text.Should().Contain("10",
            "make_sensor(9).read() must compute 9 + 1, not 0 + 1 from a never-written field");
        uno.Serial.Text.Should().Contain("25",
            "make_sensor(24).read() must compute 24 + 1, not 0 + 1 from a never-written field");
    }
}
