using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/flash-table-past-256 (pymcu-avr#14).
///
/// The flash twin of #11: the index into a flash table was loaded as one byte, so everything
/// past the first 256 bytes aliased back into them. The uint8 table is 300 bytes and pins the
/// backend half on its own, since it took this path before wide tables existed. The uint16
/// table is 400 bytes and reaches the boundary through the scaling by element size.
///
/// Against the unfixed backend this prints 1, 43, 1 and 213.
/// </summary>
[TestFixture]
public class FlashTablePast256Tests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("flash-table-past-256"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 2000);
        return uno.Serial.Text;
    }

    [Test]
    public void AByteTableOfThreeHundredReachesItsLastElements()
    {
        Boot().Should().StartWith("99\n77\n", "index 257 must not be index 1");
    }

    [Test]
    public void AWideTableCrossesTheBoundaryTheScalingReachesFirst()
    {
        Boot().Should().EndWith("4242\n777\ndone\n");
    }
}
