using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for fixtures/arena-overflow (docs/rfcs/0004-arena-allocator.md,
/// PyMCU repo). Asking the arena for far more than it reserves raises MemoryError,
/// catchable like any other builtin exception -- not a silent corruption and not an
/// unhandled halt.
/// </summary>
[TestFixture]
public class ArenaOverflowTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("arena-overflow"));

    [Test]
    public void OverflowRaisesAndIsCaught()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Be("overflow\ndone\n",
            "bytearray(n) with n far past the reservation raises MemoryError, which the " +
            "except clause catches -- 'allocated' must never print");
    }
}
