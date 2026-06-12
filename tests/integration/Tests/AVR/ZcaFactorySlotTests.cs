using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/zca-factory-slot -- RFC 0001 Model B (sret). A non-@inline
/// factory returning a MULTI-field ZCA cannot pack the instance into a return register, so it
/// uses sret: the CALLER allocates the instance's SRAM slot and passes its address as a hidden
/// __self pointer; the factory stores each field through it and returns the pointer. Two factory
/// calls get two distinct slots (no aliasing -- the caller owns each), and the default-outlined
/// method reads fields from its slot via the self pointer.
///   s = make(3,4) -> 3*4 = 12 ; t = make(5,7) -> 5*7 = 35
/// </summary>
[TestFixture]
public class ZcaFactorySlotTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("zca-factory-slot"));

    [Test]
    public void SretFactory_TwoInstances_DistinctSlots()
    {
        var uno = _session.Reset();
        uno.RunUntilSerialBytes(uno.Serial, 5, maxMs: 400); // "FS\n" (3) + 12 + 35

        var bytes = uno.Serial.Bytes;
        bytes.Length.Should().BeGreaterThanOrEqualTo(5, "banner 'FS\\n' + two results");
        bytes[^2].Should().Be(12, "s = make(3,4); read() = 3*4 = 12");
        bytes[^1].Should().Be(35, "t = make(5,7); read() = 5*7 = 35 (distinct slot, no aliasing)");
    }
}
