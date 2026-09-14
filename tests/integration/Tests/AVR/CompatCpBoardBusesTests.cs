using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-board-buses (pymcu-circuitpython#7): board.I2C(), board.SPI() and
/// board.UART() build the board's bus with the board's own pins. `i2c = board.I2C()` is the
/// first line of nearly every Adafruit sensor guide and none of the three existed.
/// </summary>
[TestFixture]
public class CompatCpBoardBusesTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-board-buses"));

    private static (byte Twbr, byte Spcr, byte Ucsrc) Run()
    {
        var uno = _session.Reset();
        uno.RunToBreak();
        return (uno.Data[Gpior0Addr], uno.Data[Gpior1Addr], uno.Data[Gpior2Addr]);
    }

    [Test]
    public void BoardI2CProgramsTheTwiAtTheDefaultRate() =>
        Run().Twbr.Should().Be(72, "100 kHz at 16 MHz");

    [Test]
    public void BoardSpiProgramsTheBusAsAControllerInModeZero() =>
        Run().Spcr.Should().Be(0x50, "SPE and MSTR set, mode 0, fosc/4");

    [Test]
    public void BoardUartProgramsAnEightNOneFrame() =>
        Run().Ucsrc.Should().Be(0x06, "eight data bits, no parity, one stop");
}
