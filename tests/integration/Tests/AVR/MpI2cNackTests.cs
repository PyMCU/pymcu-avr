using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;
using AVR8Sharp.Core.Peripherals;
using PyMCU.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/mp-i2c-nack: machine.I2C.writeto must raise
/// OSError on a NACK the way MicroPython's rp2 port reports it -- [Errno 5]
/// EIO -- instead of returning silently as if the dead bus had answered.
/// </summary>
[TestFixture]
public class MpI2cNackTests
{
    private static string _hex = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _hex = PymcuCompiler.BuildFixture("mp-i2c-nack");

    [Test]
    public void NackedAddress_PrintsOSErrorErrno5()
    {
        var uno = Sim(nackAll: true);
        uno.RunUntilSerial(uno.Serial, t => t.Contains("EIO"), maxMs: 5000);
        uno.Serial.Text.Should().Contain("nack [Errno 5] EIO",
            "MicroPython raises OSError [Errno 5] EIO on an address NACK");
    }

    [Test]
    public void AnsweredAddress_PrintsNothing()
    {
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
