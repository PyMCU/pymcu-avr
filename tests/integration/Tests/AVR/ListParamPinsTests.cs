using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A driver takes several pins as a list and keeps them in a field (PyMCU#313).
///
/// Every assertion reads a PORT register, never pin.value(). Reading the pin back lets the
/// compiler fold the write and the read into the constant it just stored, so a firmware with
/// no write to PORTD anywhere still printed the right number: three probes passed that way
/// before the oracle was moved to the register.
///
/// The seed (GPIOR0 = 1) selects an element at run time, so that dispatch is decided on the
/// chip rather than by the folder.
/// </summary>
[TestFixture]
public class ListParamPinsTests
{
    private const int Gpior0Addr = 0x3E;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("list-param-pins"));

    private string Transcript()
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = 1;
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 800);
        return uno.Serial.Text;
    }

    [Test]
    public void AForOverTheFieldDrivesEveryPin()
        => Transcript().Should().Contain("A 0\nB 28\n", "PD2, PD3 and PD4 driven high through `for p in self._pins`");

    [Test]
    public void TheSameForTurnsThemBackOff()
        => Transcript().Should().Contain("B 28\nC 0\n");

    [Test]
    public void AConstantSubscriptOfTheFieldPicksOnePin()
        => Transcript().Should().Contain("D 4\n", "self._pins[0] is PD2");

    [Test]
    public void LenOfTheFieldIsTheCount()
        => Transcript().Should().Contain("E 3\n");

    [Test]
    public void ARunTimeSubscriptSelectsAmongTheElements()
        => Transcript().Should().Contain("F 8\n", "self._pins[1] with the index from GPIOR0 is PD3");

    [Test]
    public void AListOfInstancesThatEachHoldAPin_Unrolls()
        => Transcript().Should().Contain("G 96\n", "Led(Pin(PD5)) and Led(Pin(PD6))");

    [Test]
    public void AListOfTenPinsPassedByName_DrivesBothPorts()
        => Transcript().Should().Contain("H 252 15\n").And.Contain("I 0 0\n");

    [Test]
    public void AListGivenToAMethodRatherThanTheConstructor_Unrolls()
        => Transcript().Should().Contain("J 96\n");
}
