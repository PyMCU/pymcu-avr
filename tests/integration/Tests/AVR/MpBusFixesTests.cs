using AVR8Sharp.Core.Peripherals;
using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The MicroPython layer's buses and peripherals against upstream's documented API, one
/// fixture per fix, each built through both front ends. Every value a fixture computes from
/// is read from GPIOR0 (zero at reset) or arrives on a bus, so nothing folds while compiling.
///
///   mp-i2c-repeated-start  writeto(addr, buf, False) + readfrom_into, readfrom_mem,
///                          readfrom and writevto against a device at 0x3C
///   mp-irq-nesting         disable_irq/enable_irq restore the state they found (PyMCU#353)
///   mp-adc-full-scale      read_u16 reaches 65535 at full scale
///   mp-uart-frame          bits/parity/stop, write(bytearray), readinto with timeouts
///   mp-pwm-retune          PWM(pin) then freq(f) on Timer1, the duty kept
///   mp-signal-pin-args     Signal(13, Pin.OUT, invert=True)
/// </summary>
[TestFixture]
public class MpBusFixesTests
{
    private static string Build(string fixture, bool pyParser) => pyParser
        ? PymcuCompiler.BuildFixturePyParser(fixture)
        : PymcuCompiler.BuildFixture(fixture);

    private static string RunToEnd(ArduinoUnoSimulation uno, double maxMs = 3000)
    {
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs);
        return uno.Serial.Text.Replace("\r\n", "\n");
    }

    // ── I2C ──────────────────────────────────────────────────────────────────

    /// <summary>ACKs one address and every byte written; answers reads with 0x10, 0x11, ...</summary>
    private sealed class LoggingDevice(AvrTwi twi, byte address) : ITwiEventHandler
    {
        private byte _next = 0x10;
        public List<string> Log { get; } = [];

        public void Start(bool repeated)
        {
            Log.Add(repeated ? "RSTART" : "START");
            twi.CompleteStart();
        }

        public void Stop()
        {
            Log.Add("STOP");
            twi.CompleteStop();
        }

        public void ConnectToSlave(byte addr, bool write)
        {
            Log.Add($"ADDR {addr:X2} {(write ? "W" : "R")}");
            twi.CompleteConnect(addr == address);
        }

        public void WriteByte(byte data)
        {
            Log.Add($"W {data:X2}");
            twi.CompleteWrite(true);
        }

        public void ReadByte(bool ack)
        {
            Log.Add($"R {_next:X2} {(ack ? "ACK" : "NACK")}");
            twi.CompleteRead(_next++);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void I2c_WriteWithoutStopThenRead_IsARepeatedStartAndReads(bool pyParser)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(Build("mp-i2c-repeated-start", pyParser));
        uno.AddTwi(AvrTwi.TwiConfig, out var twi);
        var dev = new LoggingDevice(twi, 0x3C);
        twi.EventHandler = dev;

        var text = RunToEnd(uno);

        text.Should().Be("16 17 18\n19 20 2\n21\n3\nEIO\nEND\n",
            "the read after a stop=False write used to raise EIO with the device present");
        var bus = string.Join(" | ", dev.Log);
        bus.Should().Contain("ADDR 3C W | W 20 | RSTART | ADDR 3C R | R 10 ACK | R 11 ACK | R 12 NACK | STOP",
            "writeto(addr, buf, False) holds the bus, so the read opens with a repeated START");
        bus.Should().Contain("ADDR 3C W | W 75 | RSTART | ADDR 3C R | R 13 ACK | R 14 NACK | STOP",
            "readfrom_mem(addr, memaddr, 2) is register write, repeated START, two reads");
        bus.Should().Contain("START | ADDR 3C W | W 40 | W A1 | W B2 | STOP",
            "writevto sends the vector's buffers back to back as one transaction");
    }

    // ── IRQ ──────────────────────────────────────────────────────────────────

    [TestCase(false)]
    [TestCase(true)]
    public void Irq_NestedSections_RestoreTheStateTheyFound(bool pyParser)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(Build("mp-irq-nesting", pyParser));
        RunToEnd(uno).Should().Be("1\n0\n0\n0\n1\nEND\n",
            "enable_irq(s2) restores 'off' while the outer section is open (PyMCU#353)");
    }

    /// <summary>
    /// The same nesting through `from machine import disable_irq, enable_irq`, the spelling
    /// most MicroPython code uses. The result of an @inline function imported with `from` was
    /// taken for an instance, so the restore's `state != 0` folded to true and the inner
    /// enable_irq(s2) re-enabled interrupts inside the outer section.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void Irq_NestedSections_FromImport_RestoreOnlyAtTheOuterSection(bool pyParser)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(Build("mp-irq-nesting-from-import", pyParser));
        RunToEnd(uno).Should().Be("1\n0\n0\n0\n1\nEND\n",
            "the inner enable_irq(s2) restores the disabled state; only the outer one re-enables");
    }

    // ── ADC ──────────────────────────────────────────────────────────────────

    [TestCase(false)]
    [TestCase(true)]
    public void Adc_ReadU16_CoversTheWholeRange(bool pyParser)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(Build("mp-adc-full-scale", pyParser));
        uno.AddAdc(AvrAdc.AdcConfig, out var adc);
        adc.ChannelValues[0] = 5.0;
        adc.ChannelValues[1] = 2.5;
        adc.ChannelValues[2] = 0.0;
        RunToEnd(uno).Should().Be("65535 32800 0\nEND\n",
            "raw << 6 | raw >> 4: 1023 is 65535 (it was 65472), 512 is 32800, 0 is 0");
    }

    // ── UART ─────────────────────────────────────────────────────────────────

    [TestCase(false)]
    [TestCase(true)]
    public void Uart_FrameWriteAndTimedReadinto(bool pyParser)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(Build("mp-uart-frame", pyParser));
        uno.RunUntilSerial(uno.Serial, "RX\n", maxMs: 500);
        uno.RunMilliseconds(5);
        uno.Serial.InjectByte(0x37);
        uno.RunMilliseconds(3);
        uno.Serial.InjectByte(0x38);

        RunToEnd(uno).Should().Be("60 6\nAB\n3\nRX\n2 55 56\nEND\n",
            "7O2 is UCSR0C 0x3C and init(9600) is 8N1 0x06; write(bytearray) returns 3; "
            + "readinto(buf, 3) returns the two bytes that arrived before timeout_char");
    }

    // ── PWM ──────────────────────────────────────────────────────────────────

    [TestCase(false)]
    [TestCase(true)]
    public void Pwm_FreqRetunesTimer1AndKeepsTheDuty(bool pyParser)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(Build("mp-pwm-retune", pyParser));
        RunToEnd(uno).Should().Be(
            "15999 8000 25\n" +   // 1 kHz default, divider 1, 50 %
            "39999 20000 26\n" +  // freq(50): divider 8, still 50 %
            "12499 6250 27\n" +   // freq(20) from GPIOR0: divider 64, still 50 %
            "20 32768\nEND\n",
            "PWM(pin) then freq(f) was refused on Timer1; it retunes ICR1 and keeps duty_u16");
    }

    // ── Signal ───────────────────────────────────────────────────────────────

    [TestCase(false)]
    [TestCase(true)]
    public void Signal_FromPinArguments_BuildsAnInvertedOutput(bool pyParser)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(Build("mp-signal-pin-args", pyParser));
        RunToEnd(uno).Should().Be("0 1 1\n1 0\n0\nEND\n",
            "Signal(13, Pin.OUT, invert=True).on() drives PB5 low with DDRB5 set; "
            + "Signal(pin, True) takes invert positionally");
    }
}
