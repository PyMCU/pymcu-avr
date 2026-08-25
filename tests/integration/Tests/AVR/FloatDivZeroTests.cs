using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/float-div-zero (PyMCU#151).
///
/// The float branch of the binary lowering returned before the divide-by-zero guard the integer
/// path has always had, so a float division by zero produced an infinity. print() renders an
/// infinity as 0.0, because it takes the integer part through uint32(), so the one value a
/// reader would take as a legitimate result is what reached the port.
///
/// GPIOR0 is seeded here, so the values are run-time ones. A fixture of literals would measure
/// the constant folder, where a literal zero divisor is now a compile error.
///
/// Against the unfixed compiler this prints 0.0 twice instead of catching.
/// </summary>
[TestFixture]
public class FloatDivZeroTests
{
    private const int Gpior0Addr = 0x3E;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("float-div-zero"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = 7;
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 4000);
        return uno.Serial.Text;
    }

    [Test]
    public void DividingAFloatByZeroRaisesInsteadOfPrintingAPlausibleZero()
    {
        Boot().Should().StartWith("caught\n", "an infinity would have printed as 0.0");
    }

    [Test]
    public void TakingAFloatModuloZeroRaisesTheSameWay()
    {
        Boot().Should().Contain("caught\ncaught-mod\n");
    }

    [Test]
    public void ADivisorThatIsNotZeroStillDivides()
    {
        Boot().Should().EndWith("3.5\n1.75\ndone\n");
    }
}
