using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A module-level object read from a function other than main (PyMCU issue #159). The build
/// used to fail naming a run-time bit index, at a line the file does not have, because those
/// functions were lowered before the construction that binds the instance. The pin has to be
/// the one the program named, driven from the function that names it.
/// </summary>
[TestFixture]
public class ModuleInstanceNonMainTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("module-instance-nonmain"));

    [Test]
    public void TurnOn_DrivesTheDeclaredPinHigh()
    {
        var uno = _session.Reset();
        uno.RunMilliseconds(10);
        uno.PortD.Should().HavePinHigh(5);
    }

    [Test]
    public void TurnOff_DrivesTheSamePinLow()
    {
        var uno = _session.Reset();
        uno.RunMilliseconds(100);
        uno.PortD.Should().HavePinLow(5);
    }

    [Test]
    public void NoOtherPinOnThePortIsTouched()
    {
        // The neighbours stay inputs: only the declared bit is configured and driven, which is
        // what the run-time mask this program used to be refused for would have put at risk.
        var uno = _session.Reset();
        uno.RunMilliseconds(10);
        uno.PortD.GetPinState(4).Should().Be(AVR8Sharp.Core.Peripherals.PinState.Input);
        uno.PortD.GetPinState(6).Should().Be(AVR8Sharp.Core.Peripherals.PinState.Input);
    }
}
