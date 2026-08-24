using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// `Pin(13, Pin.OUT)` on the bare AVR HAL (PyMCU#50).
///
/// 13 is the number on the silkscreen and the first one anyone writes, and the HAL takes a
/// port name. The rejection used to read "NotImplementedError: Unsupported Pin", which named
/// neither the pin nor the form expected, nor that board numbering exists in this project.
///
/// The message is the contract here: it must say what the HAL takes AND where the board
/// numbering lives, because both routes exist and work (pymcu.boards.arduino_uno's D13, and
/// machine.Pin's integer form).
/// </summary>
[TestFixture]
public class BoardPinNumberTests
{
    private const string PinByNumber =
        "from pymcu.hal.gpio import Pin\n" +
        "\n" +
        "\n" +
        "def main():\n" +
        "    led = Pin(13, Pin.OUT)\n" +
        "    led.high()\n";

    [Test]
    public void PinByNumber_IsRejectedWithBothRoutesNamed()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PymcuCompiler.BuildSource(PinByNumber));

        ex!.Message.Should().Contain("PB0-PB5", "the message must name the form the HAL takes");
        ex.Message.Should().Contain("arduino_uno", "and where board numbering lives");
        ex.Message.Should().Contain("machine", "and the MicroPython form that takes 13 directly");
    }
}
