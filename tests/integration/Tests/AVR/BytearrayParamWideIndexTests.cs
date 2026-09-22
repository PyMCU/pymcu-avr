using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/bytearray-param-wide-index.
///
/// A `buf: bytearray` parameter carries no element count, so BytearrayLoad/BytearrayStore
/// loaded the index as one byte and added only the carry into Z's high half -- the named-array
/// NeedsWideIndex guard (pymcu-avr#11) does not apply to a pointer param. Every offset at and
/// past 256 aliased onto the first 256 bytes: buf[256] read and wrote buf[0]. Both the
/// run-time uint16 index and a constant index past 255 are checked.
/// </summary>
[TestFixture]
public class BytearrayParamWideIndexTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("bytearray-param-wide-index"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 2000);
        return uno.Serial.Text;
    }

    [Test]
    public void WideIndex_DoesNotAliasOntoByteZero()
    {
        // buf[256] holds 0x55 and buf[0] stays 0 -- with the truncation bug buf[256] wrote
        // buf[0], so this would print "85 85 102".
        Boot().Should().StartWith("85 0 102\n",
            "a uint16 index past 255 must reach its own offset, not wrap onto buf[0]");
    }
}
