using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for fixtures/arena-in-init (docs/rfcs/0004-arena-allocator.md,
/// PyMCU repo). A runtime-sized bytearray(n) allocated as a LOCAL inside an @inline
/// __init__ constructed once at module level -- the once rule proves this the same way
/// it proves a bare module-level statement, because currentFunction stays "main"
/// through the inlining. See the fixture's own comment for why this is a local + a
/// plain field rather than `self.buf: bytearray = ...` (a separate, pre-existing gap).
/// </summary>
[TestFixture]
public class ArenaInInitTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("arena-in-init"));

    [Test]
    public void TheFirstAllocationInTheProgramGetsOffsetZero()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Be("0\ndone\n",
            "Dev's constructor is the only allocation before it runs, so arena.alloc() " +
            "returns the arena's very first offset");
    }
}
