using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for fixtures/arena-high-water (docs/rfcs/0004-arena-allocator.md,
/// PyMCU repo). `pymcu.arena.arena_high_water` is a named global an emulator test can
/// read directly: after two allocations (5 and 7 bytes) it reports 12.
/// </summary>
[TestFixture]
public class ArenaHighWaterTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("arena-high-water"));

    [Test]
    public void ReflectsTheSumOfBothAllocations()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Be("12\ndone\n", "5 bytes then 7 bytes, nothing freed");
    }
}
