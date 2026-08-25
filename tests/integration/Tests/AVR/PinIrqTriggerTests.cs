using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/pin-irq-triggers (PyMCU#142).
///
/// Which EDGE each Pin.irq trigger fires on. Pin.IRQ_HIGH_LEVEL used to fall off the end of
/// pin_irq_setup's if/elif chain, leaving EICRA at its reset value (LOW LEVEL) with
/// EIMSK enabled anyway. The reset value being itself a valid mode is what hid it: nothing
/// stated which edge a trigger was supposed to select, so selecting the wrong one and
/// selecting the right one looked identical from outside.
///
/// Pin.IRQ_CHANGE is also new here as a NAME. Trigger 3 was always implemented and is what
/// irq() defaults to; it could be written only as `Pin.IRQ_FALLING | Pin.IRQ_RISING`, which is
/// 3 by arithmetic rather than by intent.
///
/// Data-space addresses used: GPIOR1 = 0x4A, GPIOR2 = 0x4B
/// </summary>
[TestFixture]
public class PinIrqTriggerTests
{
    private SimSession _session = null!;

    private const int GPIOR1_ADDR = 0x4A;   // INT0 / PD2, armed IRQ_RISING
    private const int GPIOR2_ADDR = 0x4B;   // INT1 / PD3, armed IRQ_CHANGE

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("pin-irq-triggers"));

    /// <summary>
    /// Boots with both pins low and runs far enough for main() to arm the two interrupts.
    /// A trigger that had silently fallen through to LOW LEVEL would re-enter its ISR here
    /// for as long as the pin stayed low, so reaching this point with the counters at zero
    /// is itself part of what is being checked.
    /// </summary>
    private ArduinoUnoSimulation Armed()
    {
        var uno = _session.Reset();
        uno.PortD.SetPinValue(2, false);
        uno.PortD.SetPinValue(3, false);
        uno.RunMilliseconds(5);
        return uno;
    }

    private static void Drive(ArduinoUnoSimulation uno, byte pin, bool level)
    {
        uno.PortD.SetPinValue(pin, level);
        uno.RunMilliseconds(2);
    }

    [Test]
    public void ArmingTheInterrupts_DoesNotFireThem()
    {
        var uno = Armed();
        uno.Data[GPIOR1_ADDR].Should().Be(0, "nothing has moved either pin yet");
        uno.Data[GPIOR2_ADDR].Should().Be(0);
    }

    [Test]
    public void IrqRising_FiresOnTheRisingEdgeOnly()
    {
        var uno = Armed();

        Drive(uno, 2, true);
        uno.Data[GPIOR1_ADDR].Should().Be(1, "a low-to-high transition is a rising edge");

        Drive(uno, 2, false);
        uno.Data[GPIOR1_ADDR].Should().Be(1,
            "the falling edge must not fire an interrupt armed for the rising one");

        Drive(uno, 2, true);
        uno.Data[GPIOR1_ADDR].Should().Be(2, "and the next rising edge does fire");
    }

    [Test]
    public void IrqChange_FiresOnBothEdges()
    {
        var uno = Armed();

        Drive(uno, 3, true);
        uno.Data[GPIOR2_ADDR].Should().Be(1, "IRQ_CHANGE fires on the rising edge");

        Drive(uno, 3, false);
        uno.Data[GPIOR2_ADDR].Should().Be(2, "and on the falling edge as well");
    }

    [Test]
    public void HoldingAPinLow_DoesNotWedgeTheChip()
    {
        // The failure mode the missing guard produced: EICRA left at 0x00 is level-triggered,
        // which re-asserts for as long as the pin is low, so the ISR re-enters forever and the
        // program never reaches its next statement. Neither pin here is level-triggered, so a
        // long stretch held low must leave both counters exactly where they were.
        var uno = Armed();
        Drive(uno, 2, true);
        Drive(uno, 2, false);
        Drive(uno, 3, true);
        Drive(uno, 3, false);

        var before = (uno.Data[GPIOR1_ADDR], uno.Data[GPIOR2_ADDR]);
        uno.RunMilliseconds(50);

        (uno.Data[GPIOR1_ADDR], uno.Data[GPIOR2_ADDR]).Should().Be(before,
            "with both pins resting low and neither armed for a level, nothing may fire");
    }

    [Test]
    public void TheTwoInterruptsAreIndependent()
    {
        // INT0 and INT1 share EICRA. A setup that wrote the wrong bit pair would show up as
        // one pin's edge being counted by the other's handler.
        var uno = Armed();

        Drive(uno, 2, true);
        uno.Data[GPIOR2_ADDR].Should().Be(0, "PD2's edge belongs to INT0 alone");

        Drive(uno, 3, true);
        uno.Data[GPIOR1_ADDR].Should().Be(1, "PD3's edge must not reach INT0's counter");
        uno.Data[GPIOR2_ADDR].Should().Be(1);
    }
}
