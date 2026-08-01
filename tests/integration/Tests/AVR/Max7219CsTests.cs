using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;
using AVR8Sharp.Core.Peripherals;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/max7219-cs and fixtures/max7219-cs-default.
///
/// A MAX7219 register write is a 2-byte SPI transaction bracketed by chip-select.
/// The two transfers live in a shared subroutine that only takes primitives, so it
/// cannot see the SPI instance; CS is asserted by MAX7219._write_reg at the call site
/// through spi.select()/deselect(). These tests sample PORTB at the instant of every
/// transfer to prove the strobe lands on the pin the bus was configured with:
/// PB0 for SPI(cs="PB0"), and the hardware SS (PB2) when no cs= is given.
///
/// Regression guard: the driver used to call spi_select() directly, which is hardwired
/// to PB2, so a display wired to any other CS pin was never latched.
/// </summary>
[TestFixture]
public class Max7219CsTests
{
    // ATmega328P data-space addresses.
    private const int PORTB = 0x25;
    private const int DDRB  = 0x24;

    private const int PB0 = 1 << 0;   // cs="PB0"
    private const int PB2 = 1 << 2;   // hardware SS

    // init(): shutdown=normal, decode off, intensity 8, scan all 8, display test off.
    // set_brightness(3): intensity register again. set_row(2, 0x5A): digit register 3.
    private static readonly byte[] ExpectedCsBytes =
    [
        0x0C, 0x01,  0x09, 0x00,  0x0A, 0x08,  0x0B, 0x07,  0x0F, 0x00,
        0x0A, 0x03,
        0x03, 0x5A,
    ];

    // Same program without set_brightness().
    private static readonly byte[] ExpectedDefaultBytes =
    [
        0x0C, 0x01,  0x09, 0x00,  0x0A, 0x08,  0x0B, 0x07,  0x0F, 0x00,
        0x03, 0x5A,
    ];

    private static string _hexCs = null!;
    private static string _hexDefault = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _hexCs = PymcuCompiler.BuildFixture("max7219-cs");
        _hexDefault = PymcuCompiler.BuildFixture("max7219-cs-default");
    }

    // One SPI byte plus the state of PORTB while it was on the wire.
    private readonly record struct Transfer(byte Value, byte PortB);

    [Test]
    public void ConfiguredCs_Pb0_IsAssertedDuringEveryTransfer()
    {
        var log = new List<Transfer>();
        var uno = Sim(_hexCs, log);

        uno.RunUntilSerial(uno.Serial, "R\n", maxMs: 500);

        log.Should().NotBeEmpty("the driver must reach the SPI bus");
        log.Should().OnlyContain(t => (t.PortB & PB0) == 0,
            "cs=\"PB0\" means PB0 is the chip select and must be low while bytes are clocked out");
    }

    [Test]
    public void ConfiguredCs_LeavesHardwareSsAlone()
    {
        var log = new List<Transfer>();
        var uno = Sim(_hexCs, log);

        uno.RunUntilSerial(uno.Serial, "R\n", maxMs: 500);

        log.Should().OnlyContain(t => (t.PortB & PB2) != 0,
            "PB2 is only driven high by spi_init on a bus with an explicit cs pin -- "
            + "strobing it would be the old hardwired-SS bug");
    }

    [Test]
    public void ConfiguredCs_IsDeassertedBetweenWrites()
    {
        var uno = Sim(_hexCs, new List<Transfer>());

        // "I" is printed after init(), i.e. after the fifth write has completed.
        uno.RunUntilSerial(uno.Serial, "I\n", maxMs: 500);

        uno.PortB.Should().HavePinHigh(0, "CS returns high once a register write completes");
    }

    [Test]
    public void ConfiguredCs_PinIsConfiguredAsOutput()
    {
        var uno = Sim(_hexCs, new List<Transfer>());

        uno.RunUntilSerial(uno.Serial, "MX\n", maxMs: 300);

        (uno.Data[DDRB] & PB0).Should().Be(PB0, "SPI(cs=\"PB0\") drives PB0, so it must be an output");
    }

    [Test]
    public void ConfiguredCs_SendsExpectedRegisterSequence()
    {
        var log = new List<Transfer>();
        var uno = Sim(_hexCs, log);

        uno.RunUntilSerial(uno.Serial, "R\n", maxMs: 500);

        log.Select(t => t.Value).Should().Equal(ExpectedCsBytes,
            "each write is (register, value), in driver call order");
    }

    [Test]
    public void DefaultBus_StrobesHardwareSs()
    {
        var log = new List<Transfer>();
        var uno = Sim(_hexDefault, log);

        uno.RunUntilSerial(uno.Serial, "R\n", maxMs: 500);

        log.Should().NotBeEmpty("the driver must reach the SPI bus");
        log.Should().OnlyContain(t => (t.PortB & PB2) == 0,
            "with no cs= the chip select stays on the hardware SS (PB2)");
    }

    [Test]
    public void DefaultBus_SendsExpectedRegisterSequence()
    {
        var log = new List<Transfer>();
        var uno = Sim(_hexDefault, log);

        uno.RunUntilSerial(uno.Serial, "R\n", maxMs: 500);

        log.Select(t => t.Value).Should().Equal(ExpectedDefaultBytes);
    }

    [Test]
    public void DefaultBus_SsIsHighBetweenWrites()
    {
        var uno = Sim(_hexDefault, new List<Transfer>());

        uno.RunUntilSerial(uno.Serial, "I\n", maxMs: 500);

        uno.PortB.Should().HavePinHigh(2, "SS returns high once a register write completes");
    }

    /// <summary>
    /// Simulation whose SPI peripheral records every transmitted byte together with the
    /// PORTB output register as seen at that moment -- which pin the CS strobe landed on.
    /// </summary>
    private static ArduinoUnoSimulation Sim(string hex, List<Transfer> log)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.AddSpi(AvrSpi.SpiConfig, out var spi);
        spi.OnTransfer = b =>
        {
            log.Add(new Transfer(b, uno.Data[PORTB]));
            return 0;
        };
        return uno;
    }
}
