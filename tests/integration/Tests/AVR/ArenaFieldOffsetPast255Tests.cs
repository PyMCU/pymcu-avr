using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for fixtures/arena-field-offset-past-255 (PyMCU#418, second half).
/// A field holding a runtime-sized bytearray stores the arena OFFSET that alloc() returned.
/// The field's width was decided from the `bytearray(...)` shape and defaulted to uint8, so
/// the second allocation's offset -- 300 here -- wrapped to 44 and the second buffer
/// silently aliased the first.
/// </summary>
[TestFixture]
public class ArenaFieldOffsetPast255Tests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("arena-field-offset-past-255"));

    [Test]
    public void TheSecondBufferDoesNotAliasTheFirst()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Be("90\n0\ndone\n",
            "self.b[0] is its own byte at arena offset 300; a uint8 field wraps that to 44 " +
            "and the write shows up inside self.a");
    }
}
