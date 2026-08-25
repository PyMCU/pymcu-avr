using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/factory-returns-class (PyMCU#49).
///
/// Returning a multi-field class instance from a function lost the class, and the next method
/// call on it became "call to undefined function 'led_value'". Two pins on two ports come from
/// the same factory here, so an expansion that ignored its argument would show as both landing
/// on the same port rather than as a build that merely succeeds.
/// </summary>
[TestFixture]
public class FactoryReturnsClassTests
{
    private const int DdrBAddr = 0x24;
    private const int PortBAddr = 0x25;
    private const int DdrCAddr = 0x27;
    private const int PortCAddr = 0x28;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("factory-returns-class"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunToBreak();
        return uno;
    }

    [Test]
    public void BothPinsFromTheFactoryAreOutputsOnTheirOwnPort()
    {
        var uno = Boot();
        (uno.Data[DdrBAddr] & 0x20).Should().Be(0x20, "PB5 is an output");
        (uno.Data[DdrCAddr] & 0x02).Should().Be(0x02, "PC1 is an output, not a second PB5");
    }

    [Test]
    public void EachPinCarriesItsOwnLevel()
    {
        var uno = Boot();
        (uno.Data[PortBAddr] & 0x20).Should().Be(0x20, "led.value(1)");
        (uno.Data[PortCAddr] & 0x02).Should().Be(0, "other.value(0)");
    }
}
