using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// `Pin(13, Pin.OUT)` on the bare AVR HAL (PyMCU#50).
///
/// 13 is the number on the silkscreen and the first one anyone writes; the HAL took only a
/// port name, so the first line of the first program was a compile error. The board number
/// is now an alternative in the same compile-time match as the port name, which is why the
/// two spellings can be asserted to emit the same firmware byte for byte.
///
/// The levels driven come from GPIOR0, seeded by the test, so what is measured is the pin
/// mapping and not the constant folder.
/// </summary>
[TestFixture]
public class BoardPinNumberTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int DdrBAddr = 0x24;
    private const int PortBAddr = 0x25;
    private const int DdrCAddr = 0x27;
    private const int PortCAddr = 0x28;
    private const int DdrDAddr = 0x2A;
    private const int PortDAddr = 0x2B;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("board-pin-number"));

    private ArduinoUnoSimulation RunWithSeed(byte seed)
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = seed;
        uno.RunToBreak();
        return uno;
    }

    [Test]
    public void BoardNumbers_SetTheDirectionOfTheirOwnPort()
    {
        var uno = RunWithSeed(0);
        (uno.Data[DdrBAddr] & 0x20).Should().Be(0x20, "Pin(13, Pin.OUT) is PB5");
        (uno.Data[DdrCAddr] & 0x02).Should().Be(0x02, "Pin(15, Pin.OUT) is A1 = PC1");
        (uno.Data[DdrDAddr] & 0x80).Should().Be(0x80, "Pin(7, Pin.OUT) is PD7");
        (uno.Data[DdrDAddr] & 0x04).Should().Be(0, "Pin(2, Pin.IN_PULLUP) leaves PD2 an input");
        (uno.Data[PortDAddr] & 0x04).Should().Be(0x04, "Pin(2, Pin.IN_PULLUP) turns PD2's pull-up on");
    }

    // Both seeds, and the pins the seed leaves low are asserted too: a mapping that drove
    // every port, or ignored the number and always picked PB5, passes a one-bit check.
    [Test]
    public void SeedBits_ReachTheirOwnBoardPin()
    {
        var uno = RunWithSeed(0b101);
        (uno.Data[PortBAddr] & 0x20).Should().Be(0x20, "bit 0 of the seed drives pin 13 = PB5");
        (uno.Data[PortCAddr] & 0x02).Should().Be(0, "bit 1 is clear, so pin 15 = PC1 stays low");
        (uno.Data[PortDAddr] & 0x80).Should().Be(0x80, "bit 2 drives pin 7 = PD7");
    }

    [Test]
    public void SeedBits_ReachTheirOwnBoardPin_Complement()
    {
        var uno = RunWithSeed(0b010);
        (uno.Data[PortBAddr] & 0x20).Should().Be(0, "bit 0 is clear, so pin 13 = PB5 stays low");
        (uno.Data[PortCAddr] & 0x02).Should().Be(0x02, "bit 1 drives pin 15 = PC1");
        (uno.Data[PortDAddr] & 0x80).Should().Be(0, "bit 2 is clear, so pin 7 = PD7 stays low");
    }

    [TestCase((byte)0b101, (byte)0xFA)]
    [TestCase((byte)0b010, (byte)0xFD)]
    public void TheSeedWasActuallyRead(byte seed, byte expected)
        => RunWithSeed(seed).Data[Gpior1Addr].Should().Be(expected,
            "GPIOR1 carries ~seed, so a run that never loaded GPIOR0 is visible");

    // The claim the implementation makes is that the number costs nothing: it is an
    // alternative in the same match arm as the port name, so both spellings fold to the
    // same registers before codegen sees them.
    [Test]
    public void NumberAndPortName_CompileToTheSameFirmware()
    {
        const string byNumber =
            "from pymcu.hal.gpio import Pin\n\n\ndef main():\n    led = Pin(13, Pin.OUT)\n    led.high()\n\n\nmain()\n";
        const string byPortName =
            "from pymcu.hal.gpio import Pin\n\n\ndef main():\n    led = Pin(\"PB5\", Pin.OUT)\n    led.high()\n\n\nmain()\n";

        PymcuCompiler.BuildSource(byNumber).Should().Be(PymcuCompiler.BuildSource(byPortName));
    }

    [Test]
    public void ANumberWithNoPinBehindIt_IsStillRejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PymcuCompiler.BuildSource(
            "from pymcu.hal.gpio import Pin\n\n\ndef main():\n    led = Pin(99, Pin.OUT)\n    led.high()\n\n\nmain()\n"));

        ex!.Message.Should().Contain("PB0-PB5", "the message must name the port names the HAL takes");
        ex.Message.Should().Contain("0-19", "and the board numbers, now that they are accepted");
    }
}
