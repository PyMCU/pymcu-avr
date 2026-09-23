using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/avr/compat-mp-pin-mode-literals and
/// fixtures/avr/compat-mp-pin-mode-symbols.
///
/// machine.Pin's mode/trigger constants carry the upstream MicroPython rp2
/// values (IN=0, OUT=1, IRQ_FALLING=4, IRQ_RISING=8) while every PyMCU GPIO
/// HAL numbers directions the other way around (IN=1, OUT=0) and numbers its
/// triggers 1..4. The compat layer translates once at the HAL call, so a
/// program that writes the literal ints MicroPython documents and the same
/// program written with the named constants must compile to byte-identical
/// firmware — under both compiler front ends — and must show the upstream
/// semantics on the pins (literal 0 is an input, literal 1 an output,
/// literal 4 a falling-edge IRQ).
/// </summary>
[TestFixture]
public class CompatMpPinModeValuesTests
{
    // ATmega328P data-space addresses (I/O addr + 0x20)
    private const int DDRB  = 0x24;
    private const int PORTB = 0x25;
    private const int DDRD  = 0x2A;
    private const int PORTD = 0x2B;
    private const int EICRA = 0x69;
    private const int EIMSK = 0x3D;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("compat-mp-pin-mode-literals"));

    [Test]
    public void LiteralAndSymbolic_BuildIdenticalHex()
    {
        var literal  = PymcuCompiler.BuildFixture("compat-mp-pin-mode-literals");
        var symbolic = PymcuCompiler.BuildFixture("compat-mp-pin-mode-symbols");
        symbolic.Should().Be(literal,
            "machine.Pin's constants are the upstream values, so Pin(13, 1) and " +
            "Pin(13, Pin.OUT) are the same call and must emit the same image");
    }

    [Test]
    public void LiteralAndSymbolic_BuildIdenticalHex_PyParser()
    {
        var literal  = PymcuCompiler.BuildFixturePyParser("compat-mp-pin-mode-literals");
        var symbolic = PymcuCompiler.BuildFixturePyParser("compat-mp-pin-mode-symbols");
        symbolic.Should().Be(literal,
            "the Python front end must lower the same literals-vs-constants equivalence");
    }

    [Test]
    public void LiteralOne_ConfiguresOutput()
    {
        var uno = Sim();
        uno.RunMilliseconds(10);
        (uno.Data[DDRB] & 0x20).Should().NotBe(0,
            "Pin(13, 1): literal 1 is upstream OUT, so DDRB5 is set");
        (uno.Data[PORTB] & 0x20).Should().NotBe(0,
            "out.high() drives PB5");
    }

    [Test]
    public void LiteralZero_ConfiguresInputWithPullUp()
    {
        var uno = Sim();
        uno.RunMilliseconds(10);
        (uno.Data[DDRD] & 0x04).Should().Be(0,
            "Pin(2, 0, Pin.PULL_UP): literal 0 is upstream IN, so DDRD2 is clear");
        (uno.Data[PORTD] & 0x04).Should().NotBe(0,
            "the pull-up latch on PD2 is set");
    }

    [Test]
    public void LiteralInitZero_ReconfiguresToInput()
    {
        var uno = Sim();
        uno.RunMilliseconds(10);
        (uno.Data[DDRD] & 0x10).Should().Be(0,
            "aux.init(0): literal 0 is upstream IN, so DDRD4 ends clear");
    }

    [Test]
    public void LiteralFour_SelectsFallingEdgeIrq()
    {
        var uno = Sim();
        uno.RunMilliseconds(10);
        (uno.Data[EICRA] & 0x03).Should().Be(0x02,
            "inp.irq(on_btn, 4): literal 4 is upstream IRQ_FALLING -> ISC01:ISC00 = 10");
        (uno.Data[EIMSK] & 0x01).Should().Be(0x01, "INT0 is enabled");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
