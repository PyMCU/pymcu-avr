using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-busio-parameters (pymcu-circuitpython#22, #23, #24): the parameters
/// busio takes reach the hardware. Before, bits, parity, stop, frequency, polarity and phase
/// were accepted and dropped: a UART asked for 7E2 ran 8N1, a bus asked for 400 kHz ran at
/// 100 kHz, and a display asked for mode 3 at 1 MHz ran mode 0 at 4 MHz, all silently.
/// </summary>
[TestFixture]
public class CompatCpBusioParametersTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-busio-parameters"));

    private static (byte G0, byte G1)[] Run()
    {
        var uno = _session.Reset();
        var reads = new (byte, byte)[3];
        for (var i = 0; i < reads.Length; i++)
        {
            if (i > 0) uno.RunInstructions(1);
            uno.RunToBreak();
            reads[i] = (uno.Data[Gpior0Addr], uno.Data[Gpior1Addr]);
        }
        return reads;
    }

    [Test]
    public void TheUartFrameIsTheOneAskedFor()
    {
        var ucsrc = Run()[0].G0;
        ((ucsrc >> 4) & 0b11).Should().Be(0b10, "even parity is UPM 10");
        ((ucsrc >> 3) & 1).Should().Be(1, "stop=2 is USBS 1");
        ((ucsrc >> 1) & 0b11).Should().Be(0b10, "seven data bits is UCSZ 10");
        ucsrc.Should().Be(0x2C, "7E2 is the whole register, not 8N1's 0x06");
    }

    [Test]
    public void TheI2CBitRateIsTheFrequencyAskedFor()
    {
        // SCL = F_CPU / (16 + 2 * TWBR), so 400 kHz at 16 MHz is TWBR 12. It was 72, the
        // literal for 100 kHz, whatever the program asked for.
        Run()[1].G0.Should().Be(12);
    }

    [Test]
    public void TheSpiModeAndClockAreTheOnesConfigured()
    {
        var (spcr, spsr) = Run()[2];
        ((spcr >> 3) & 1).Should().Be(1, "polarity=1 is CPOL");
        ((spcr >> 2) & 1).Should().Be(1, "phase=1 is CPHA");
        (spcr & 0b11).Should().Be(0b01, "1 MHz at 16 MHz is fosc/16, which is SPR 01");
        (spsr & 1).Should().Be(0, "fosc/16 leaves SPI2X clear");
        spcr.Should().Be(0x5D, "mode 3 at fosc/16, not mode 0 at fosc/4's 0x50");
    }
}
