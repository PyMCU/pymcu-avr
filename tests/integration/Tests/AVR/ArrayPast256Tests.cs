using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/array-past-256 (pymcu-avr#11).
///
/// A run-time index was loaded as one byte, so everything past the first 256 bytes aliased
/// back into them. The indices straddle the boundary on purpose: 255 is the last the old path
/// could reach and 256 the first it could not, and a uint16 array is checked at 127 and 128,
/// where the doubling of an 8-bit index overflowed.
/// </summary>
[TestFixture]
public class ArrayPast256Tests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("array-past-256"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 2000);
        return uno.Serial.Text;
    }

    [Test]
    public void EveryByteOfAFiveHundredByteArrayIsItsOwn()
    {
        Boot().Should().StartWith("11 22 33 44\n", "255 and 256 must not be the same slot");
    }

    [Test]
    public void AUint16ArrayDoesNotWrapAtIndex128()
    {
        Boot().Should().Contain("55 66\ndone\n");
    }
}
