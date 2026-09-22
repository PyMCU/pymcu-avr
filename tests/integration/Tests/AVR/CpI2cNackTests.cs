using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;
using AVR8Sharp.Core.Peripherals;
using PyMCU.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/cp-i2c-nack: busio.I2C.writeto must raise
/// OSError on a NACK, the way CircuitPython's RP2040 port does -- [Errno 19]
/// No such device when the address goes unanswered.
///
/// This is the field report the rescue answers: an Uno driving an SSD1306 that
/// stayed dark while `try/except Exception` around the driver construction
/// never ran, because writeto ignored the TWI status. With the bus NACKing
/// every address the except handler must now fire and print the OSError text;
/// on a bus that answers, the write succeeds and nothing prints.
/// </summary>
[TestFixture]
public class CpI2cNackTests
{
    private static string _hex = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _hex = PymcuCompiler.BuildFixture("cp-i2c-nack");

    [Test]
    public void NackedAddress_PrintsOSErrorErrno19()
    {
        var uno = Sim(nackAll: true);
        uno.RunUntilSerial(uno.Serial, t => t.Contains("No such device"), maxMs: 5000);
        uno.Serial.Text.Should().Contain("nack [Errno 19] No such device",
            "CircuitPython raises OSError [Errno 19] on an address NACK");
    }

    [Test]
    public void AnsweredAddress_PrintsNothing()
    {
        // The recorder ACKs 0x3C: the write succeeds and the except body never runs.
        var uno = Sim(nackAll: false);
        uno.RunMilliseconds(500);
        uno.Serial.Text.Should().NotContain("nack",
            "an ACKed transaction raises nothing");
    }

    private static ArduinoUnoSimulation Sim(bool nackAll)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(_hex);
        uno.AddTwi(AvrTwi.TwiConfig, out var twi);
        twi.EventHandler = new TwiRecorder(twi, 0x3C, nackAll);
        return uno;
    }
}
