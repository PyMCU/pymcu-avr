using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// RFC 0012, grouped peripherals, on fixtures/grouped-peripheral-timer1 and its
/// -loose twin.
///
/// A chip definition exposes every special function register as a loose module-level
/// name. Those names are how the HAL reaches the silicon and they follow whatever the
/// HAL needs, so they are not a surface to write programs against. The grouped form
/// declares the same registers as attributes of a class named after the peripheral,
/// the way an XC8 program reaches T1CON through the Timer1 SFR block:
///
///     TIMER1.TCCR1B.value = (1 &lt;&lt; TIMER1.WGM13) | (1 &lt;&lt; TIMER1.CS10)
///
/// Two things are measured here and both are needed. The first is that the grouping
/// costs nothing: the two fixtures are the same program in the two spellings and
/// their firmware must be identical byte for byte, which is what the last test says.
/// The second is that the grouped program actually drives the hardware, because
/// identical-to-a-program-that-does-nothing would also be identical.
///
/// The fixture runs Timer/Counter1 in Fast PWM mode 14 with TOP = ICR1 = 19999 at
/// prescaler 1, so one period is 20000 cycles = 1.25 ms at 16 MHz.
///
/// Checkpoint 1: configured, nothing counted yet.
/// Checkpoint 2: three overflows seen and cleared, counted into GPIOR0.
/// Checkpoint 3: clock stopped, the counter given 0x1234 and read back through the
///               16-bit name and through both byte names. The value is written rather
///               than sampled because the counter a free run leaves behind depends on
///               how many cycles the build took, which is exactly what the optimizer
///               and peephole differential axes vary.
///
/// Every 16-bit register is WRITTEN through its byte names, high half first, because the
/// AVR commits such a register when the low byte is written and takes the high half from
/// a temporary register shared by the whole timer. That is what the HAL does and what the
/// fixture does, so the timer really runs at the period asserted below. Reading is the
/// other order and the 16-bit name is correct for it, which checkpoint 3 exercises.
///
/// Data-space addresses (ATmega328P): TIFR1 0x36, TIMSK1 0x6F, TCCR1A 0x80,
/// TCCR1B 0x81, TCNT1 0x84, ICR1 0x86, OCR1A 0x88, OCR1B 0x8A, GPIOR0 0x3E, DDRB 0x24.
/// </summary>
[TestFixture]
public class GroupedPeripheralTimer1Tests
{
    private SimSession _session = null!;

    private const int DDRB_ADDR   = 0x24;
    private const int TIFR1_ADDR  = 0x36;
    private const int GPIOR0_ADDR = 0x3E;
    private const int GPIOR1_ADDR = 0x4A;
    private const int GPIOR2_ADDR = 0x4B;
    private const int TIMSK1_ADDR = 0x6F;
    private const int TCCR1A_ADDR = 0x80;
    private const int TCCR1B_ADDR = 0x81;
    private const int TCNT1_ADDR  = 0x84;
    private const int ICR1_ADDR   = 0x86;
    private const int OCR1A_ADDR  = 0x88;
    private const int OCR1B_ADDR  = 0x8A;

    // Three PWM periods of 20000 cycles each, plus the polling loop around them.
    private const int OverflowBudget = 5_000_000;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("grouped-peripheral-timer1"));

    private ArduinoUnoSimulation BootCp1()
    {
        var uno = _session.Reset();
        uno.RunToBreak();
        return uno;
    }

    private ArduinoUnoSimulation BootCp2()
    {
        var uno = BootCp1();
        uno.RunInstructions(1);
        uno.RunToBreak(OverflowBudget);
        return uno;
    }

    private ArduinoUnoSimulation BootCp3()
    {
        var uno = BootCp2();
        uno.RunInstructions(1);
        uno.RunToBreak(OverflowBudget);
        return uno;
    }

    // --- Checkpoint 1: the group wrote the configuration ---

    [Test]
    public void Cp1_TCCR1A_HasTheModeAndOutputBitsWrittenByName()
    {
        // (1 << COM1A1) | (1 << WGM11) = 0x80 | 0x02
        BootCp1().Data[TCCR1A_ADDR].Should().Be(0x82,
            "TIMER1.TCCR1A.value took the bit positions named in the group");
    }

    [Test]
    public void Cp1_TCCR1B_SelectsModeFourteenAtPrescalerOne()
    {
        // (1 << WGM13) | (1 << WGM12) | (1 << CS10) = 0x10 | 0x08 | 0x01
        BootCp1().Data[TCCR1B_ADDR].Should().Be(0x19,
            "WGM13:12 = 11 with CS10 is Fast PWM with TOP = ICR1 and no prescaling");
    }

    [Test]
    public void Cp1_SixteenBitRegisters_HoldTheirWholeValues()
    {
        var uno = BootCp1();
        uno.Memory.Should().HaveWordAt(ICR1_ADDR, 19999, "TOP, high half written first");
        uno.Memory.Should().HaveWordAt(OCR1A_ADDR, 1500, "OC1A duty, high half written first");
        uno.Memory.Should().HaveWordAt(OCR1B_ADDR, 1000, "OC1B duty, high half written first");
    }

    [Test]
    public void Cp1_TIMSK1_LeavesTheOverflowInterruptMasked()
    {
        BootCp1().Data[TIMSK1_ADDR].Should().Be(0x00,
            "the fixture polls TOV1, so no Timer1 interrupt may be enabled");
    }

    [Test]
    public void Cp1_DDRB_DrivesPB1()
    {
        BootCp1().Data[DDRB_ADDR].Should().Be(0x02, "OC1A is PB1 and the fixture makes it an output");
    }

    // --- Checkpoint 2: the timer ran and the flag was read and cleared by name ---

    [Test]
    public void Cp2_ThreeOverflowsWereSeenAndCounted()
    {
        BootCp2().Data[GPIOR0_ADDR].Should().Be(3,
            "the loop leaves only after TIMER1.TIFR1[TIMER1.TOV1] has been seen set three times");
    }

    [Test]
    public void Cp2_TheOverflowFlagWasClearedByWritingAOne()
    {
        // The last clear happens inside the iteration that reaches three, so the flag
        // must be down when the loop leaves.
        (BootCp2().Data[TIFR1_ADDR] & 0x01).Should().Be(0,
            "TOV1 is cleared by writing a ONE to it, which is what TIFR1[TOV1] = 1 emits");
    }

    // --- Checkpoint 3: the counter read back at both widths ---

    [Test]
    public void Cp3_TheClockIsStopped()
    {
        BootCp3().Data[TCCR1B_ADDR].Should().Be(0x18,
            "the CS bits were cleared, so the counter holds still for the read-back");
    }

    [Test]
    public void Cp3_TheSixteenBitReadReturnedTheWholeCounter()
    {
        // The comparison runs on the chip, so the marker only appears when the 16-bit
        // read brought back both halves. A swapped pair reads 0x3412 and a single-byte
        // read 0x0034; neither is 0x1234.
        BootCp3().Data[GPIOR0_ADDR].Should().Be(0xA5,
            "TIMER1.TCNT1.value == 0x1234 is what the marker is written under");
    }

    [Test]
    public void Cp3_TheByteNamesReadTheTwoHalves()
    {
        var uno = BootCp3();
        uno.Data[GPIOR1_ADDR].Should().Be(0x34, "TIMER1.TCNT1L.value is the low half");
        uno.Data[GPIOR2_ADDR].Should().Be(0x12, "TIMER1.TCNT1H.value is the high half");
    }

    // --- The bar: grouping costs nothing ---

    [Test]
    public void TheGroupedAndLooseFixtures_ProduceIdenticalFirmware()
    {
        string grouped = PymcuCompiler.BuildFixture("grouped-peripheral-timer1");
        string loose = PymcuCompiler.BuildFixture("grouped-peripheral-timer1-loose");

        grouped.Should().Be(loose,
            "the grouped spelling is a re-grouping of the same registers, so it may not "
            + "cost a single byte over the loose names");
    }
}
