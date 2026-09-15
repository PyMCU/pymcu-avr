using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for fixtures/arena-module-level (docs/rfcs/0004-arena-allocator.md,
/// PyMCU repo). A `bytearray(n)` allocation with a runtime n (GPIOR0-seeded, so n = 5)
/// at module level: writes through the buffer, reads back, and reports len().
/// </summary>
[TestFixture]
public class ArenaModuleLevelTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("arena-module-level"));

    [Test]
    public void WritesReadsAndLenAllAgree()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Be("11\n22\n5\ndone\n",
            "buf[0]=11 and buf[1]=22 were written through the arena offset, both read back " +
            "correctly, and len(buf) reports the allocated size (5), not a static capacity");
    }
}
