using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/escaping-array (pymcu-avr: call-tree overlay rescue).
///
/// StackAllocator overlays the locals of sibling call subtrees: two calls main() makes
/// in sequence reuse one SRAM region. That is sound only while every name in the
/// region dies with its call. The fixture's `self.buf = bytearray(64)` creates an
/// array inside the inlined `__init__` that the object then retains; the same array
/// symbol is named by fill() and check(). Unfixed, the buffer sat inside the region
/// clobber() reuses, so clobber()'s stores overwrote it. The backend now promotes an
/// array whose storage outlives one frame into the global section, where the overlay
/// can never reach it.
///
/// Expected UART output:
///   "0\n"    -- every buffer byte survived clobber()
///   "done\n" -- completion marker
/// </summary>
[TestFixture]
public class EscapingArrayTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("escaping-array"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 3000);
        return uno.Serial.Text;
    }

    [Test]
    public void InstanceRetainedArraySurvivesASiblingFrame()
    {
        string output = Boot();
        output.Should().Be("0\ndone\n");
    }
}
