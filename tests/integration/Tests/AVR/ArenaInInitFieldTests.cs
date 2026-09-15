using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for fixtures/arena-in-init-field (docs/rfcs/0004-arena-allocator.md,
/// PyMCU repo). `self.buf = bytearray(n)`, the obvious spelling for a runtime-sized
/// field, works now that PyMCU#392 landed. Writes and reads back three indices through
/// `self.buf[i]` (via two more @inline methods, poke()/peek()) and reports
/// `len(self.buf)` -- PyMCU#418, originally a silent bit-operation miscompile, fixed on
/// this branch. See fixtures/arena-in-init for the local-variable spelling this
/// generalizes, kept alongside this one.
/// </summary>
[TestFixture]
public class ArenaInInitFieldTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("arena-in-init-field"));

    [Test]
    public void WritesReadsAndLenAllAgreeThroughTheField()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Be("11\n22\n33\n4\ndone\n",
            "three bytes written through self.buf[i] read back correctly through the " +
            "same field, and len(self.buf) reports the allocated size (4), not a static " +
            "capacity");
    }
}
