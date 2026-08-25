using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/flash-table-wide (PyMCU#135).
///
/// Only const[uint8[N]] was recognised as a flash array, so a wider table was never emitted:
/// the subscript lowered to a register bit test on a scalar that does not exist, every read
/// folded to zero, and a run-time index failed the build talking about bit indices.
///
/// Against the unfixed compiler this fixture does not build, because of the run-time index at
/// the end. Remove that line and it builds clean and prints 0 for the three wide tables.
/// </summary>
[TestFixture]
public class FlashTableWideTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("flash-table-wide"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 2000);
        return uno.Serial.Text;
    }

    [Test]
    public void AWideTableHoldsItsValues()
    {
        Boot().Should().StartWith("300\n", "uint16 elements must survive into flash");
    }

    [Test]
    public void ASignedTableComesBackSigned()
    {
        Boot().Should().Contain("300\n-5\n", "-5 must not read back as 65531");
    }

    [Test]
    public void AUint32TableHoldsAValueNoNarrowerTypeCouldHold()
    {
        Boot().Should().Contain("70000\n");
    }

    [Test]
    public void AByteTableStillWorksAndARuntimeIndexBuilds()
    {
        Boot().Should().EndWith("66\n65\ndone\n");
    }
}
