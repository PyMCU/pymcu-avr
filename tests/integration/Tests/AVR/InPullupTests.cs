using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/in-pullup (PyMCU#59).
///
/// `Pin.IN_PULLUP` is Arduino's INPUT_PULLUP and MicroPython's `Pin.IN, Pin.PULL_UP`. It is
/// the second line of every button program, because the internal pull-up is what lets a
/// button work without an external resistor.
///
/// Two claims: the registers say input-with-pull-up, and the new spelling is the SAME image
/// as the long one, so the constant is an alias rather than a second implementation.
/// </summary>
[TestFixture]
public class InPullupTests
{
    private const int DdrbAddr = 0x24;
    private const int PortbAddr = 0x25;
    private const int Pb0 = 1 << 0;

    private const string LongSpelling =
        "from pymcu.hal.gpio import Pin\n" +
        "from pymcu.types import asm\n" +
        "from pymcu.chips.atmega328p import GPIOR0\n" +
        "\n" +
        "\n" +
        "def main():\n" +
        "    boton = Pin(\"PB0\", Pin.IN, Pin.PULL_UP)\n" +
        "    GPIOR0.value = boton.value()\n" +
        "    asm(\"BREAK\")\n" +
        "    while True:\n" +
        "        pass\n";

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("in-pullup"));

    [Test]
    public void InPullup_LeavesThePinAnInputWithThePullUpOn()
    {
        var uno = _session.Reset();
        uno.RunToBreak();

        (uno.Data[DdrbAddr] & Pb0).Should().Be(0, "IN_PULLUP is an input");
        (uno.Data[PortbAddr] & Pb0).Should().Be(Pb0, "and the pull-up must be enabled");
    }

    [Test]
    public void InPullup_IsTheSameImageAsTheLongSpelling()
    {
        PymcuCompiler.BuildFixture("in-pullup").Should().Be(PymcuCompiler.BuildSource(LongSpelling),
            "the constant is an alias for IN + PULL_UP, not a second implementation");
    }
}
