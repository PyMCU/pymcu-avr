using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/float-print-large (PyMCU#99).
///
/// uart_write_float scaled the whole value by 100 into a uint32 before producing a digit, so
/// everything past that accumulator printed as the same saturated number: 1e8 and 1e9 both
/// came out as 21474836.48. The value itself was never wrong, only the printing, which is
/// what made it look like a conversion problem.
///
/// The small values are pinned as well: the fix changes how every float is printed, not only
/// the large ones.
/// </summary>
[TestFixture]
public class FloatPrintLargeTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("float-print-large"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 4000);
        return uno.Serial.Text;
    }

    [Test]
    public void SmallValuesAreUnchanged()
    {
        Boot().Should().StartWith("3.5\n0.75\n-7.0\n0.01\n");
    }

    [Test]
    public void RoundingStillCarriesOutOfTheFraction()
    {
        Boot().Should().Contain("1.0\n10.0\n0.0\n",
            "0.999 is 1.00 and 9.999 is 10.00, never 0.100 and 9.100");
    }

    [Test]
    public void AValuePastTheOldAccumulatorPrintsItself()
    {
        Boot().Should().Contain("100000000.0\n1000000000.0\n",
            "both of these used to print 21474836.48");
    }

    [Test]
    public void TheSaturatedNumberIsGoneEntirely()
    {
        Boot().Should().NotContain("21474836.48");
    }

    [Test]
    public void ALargeNegativeKeepsItsSignAndItsDigits()
    {
        Boot().Should().Contain("-1000000000.0\n");
    }
}
