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
    private const int PortcAddr = 0x28;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-board-buses"));

    private static (byte Twbr, byte Spcr, byte Ucsrc, byte Portc) Run()
    {
        var uno = _session.Reset();
        uno.RunToBreak();
        return (uno.Data[Gpior0Addr], uno.Data[Gpior1Addr], uno.Data[Gpior2Addr], uno.Data[PortcAddr]);
    }

    [Test]
    public void BoardI2CProgramsTheTwiAtTheDefaultRate() =>
        Run().Twbr.Should().Be(72, "100 kHz at 16 MHz");

    [Test]
    public void BoardI2CTurnsOnTheInternalPullUpsLikeArduino() =>
        (Run().Portc & 0x30).Should().Be(0x30,
            "PC4 (SDA) and PC5 (SCL) get their internal pull-ups, as twi_init() leaves them");

    [Test]
    public void BoardI2CWithSdaHeldLow_IsCircuitPythonsWiringError()
    {
        // A line that reads low with the pull-ups on is a bus with nothing
        // pulling it up; CircuitPython's busio.I2C refuses it at construction
        // with RuntimeError, which prints E:RuntimeError on the UART and halts.
        var uno = _session.Reset();
        uno.PortC.SetPinValue(4, false);   // SDA held low
        uno.RunUntilSerial(uno.Serial, t => t.Contains("RuntimeError"), maxMs: 2000);
        uno.Serial.Text.Should().Contain("E:RuntimeError");
    }

    // The exact message is CircuitPython's own text, quoted from
    // shared-module busio I2C on the ports that keep CIRCUITPY_REQUIRE_I2C_PULLUPS.
    private const string WiringCheckSrc = """
import board, busio


def main():
    try:
        i2c = busio.I2C(board.SCL, board.SDA)
        print("constructed")
    except RuntimeError as e:
        print(e.args[0])
""";

    private const string CpPyproject = """
[project]
name = "wiring-check"
version = "0.1.0"
requires-python = ">=3.11"
dependencies = ["pymcu-stdlib>=0.1.2a5", "pymcu>=0.1.0a27", "pymcu-circuitpython>=0.1.0"]

[tool.pymcu]
board     = "arduino_uno"
frequency = 16000000
sources   = "src"
entry     = "main.py"
stdlib    = ["circuitpython"]
""";

    [Test]
    public void BusioI2CWithSclHeldLow_PrintsTheExactUpstreamMessage()
    {
        var hex = PymcuCompiler.BuildSource(WiringCheckSrc, CpPyproject);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.PortC.SetPinValue(4, true);    // SDA up
        uno.PortC.SetPinValue(5, false);   // SCL held low
        uno.RunUntilSerial(uno.Serial, t => t.Contains("wiring"), maxMs: 2000);
        uno.Serial.Text.Should().Contain("No pull up found on SDA or SCL; check your wiring");
    }

    [Test]
    public void BusioI2CWithLinesHigh_Constructs()
    {
        var hex = PymcuCompiler.BuildSource(WiringCheckSrc, CpPyproject);
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.PortC.SetPinValue(4, true);    // SDA
        uno.PortC.SetPinValue(5, true);    // SCL
        uno.RunUntilSerial(uno.Serial, "constructed\n", maxMs: 2000);
        uno.Serial.Text.Should().Contain("constructed");
    }

    [Test]
    public void BoardSpiProgramsTheBusAsAControllerInModeZero() =>
        Run().Spcr.Should().Be(0x50, "SPE and MSTR set, mode 0, fosc/4");

    [Test]
    public void BoardUartProgramsAnEightNOneFrame() =>
        Run().Ucsrc.Should().Be(0x06, "eight data bits, no parity, one stop");
}
