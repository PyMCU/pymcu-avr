using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for fixtures/arena-in-init-field (docs/rfcs/0004-arena-allocator.md,
/// PyMCU repo). `self.buf = bytearray(n)`, the obvious spelling for a runtime-sized
/// field, works now that PyMCU#392 landed. Does not exercise `self.buf[i]` bracket
/// indexing (PyMCU#418: silently compiles to a bit operation, not a byte access) --
/// see fixtures/arena-in-init for the local-variable spelling that avoids it.
/// </summary>
[TestFixture]
public class ArenaInInitFieldTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("arena-in-init-field"));

    [Test]
    public void TheFirstAllocationInTheProgramGetsOffsetZero()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Be("0\ndone\n",
            "Dev's constructor is the only allocation before it runs, so arena.alloc() " +
            "returns the arena's very first offset, stored in self.buf");
    }
}
