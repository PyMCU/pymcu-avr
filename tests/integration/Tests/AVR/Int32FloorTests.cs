using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/int32-floor (PyMCU#120).
///
/// `x: int32 = -2147483648` used to fail the build, reporting the number as out of range for
/// the type whose minimum it is. int32 is the widest integer type PyMCU has, so there was no
/// wider annotation to fall back on and no cast that avoids the literal.
///
/// The compiler unit tests pin the diagnostic. This pins the value, which they cannot: the
/// literal being accepted says nothing about the four bytes that reach the chip.
/// </summary>
[TestFixture]
public class Int32FloorTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("int32-floor"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 4000);
        return uno.Serial.Text;
    }

    [Test]
    public void TheFloorReachesTheChipAsItself()
    {
        Boot().Should().StartWith("-2147483648\n");
    }

    [Test]
    public void TheSubtractionWorkaroundStillGivesTheSameValue()
    {
        Boot().Should().StartWith("-2147483648\n-2147483648\n",
            "-2147483647 - 1 built while #120 was open and must keep building");
    }

    [Test]
    public void TheOtherEdgesAreUnchanged()
    {
        Boot().Should().Contain("2147483647\n4294967295\n-32768\n-128\n",
            "the ceiling was never the broken side, and the narrower floors always worked");
    }
}
