using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;
using AVR8Sharp.Core.Peripherals;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/avr/compat-mp-i2c-big-buffer.
///
/// `machine.I2C.writeto(addr, buf)` sends `len(buf)` bytes, and `len(buf)` is allowed to be
/// larger than 255. The AVR entry point declared its count `uint8`, so the count truncated
/// at the call: 300 went out as `300 &amp; 0xFF == 44` and 512 -- the framebuffer of a 128x32
/// SSD1306 -- went out as zero, so `show()` put one byte on the bus and the display stayed
/// blank while every command byte before it was correct (PyMCU#511).
///
/// Nothing in the source says 300: the count comes from `len()`, so the compiler's refusal
/// of a literal too wide for its parameter cannot see it. That is what makes this worth a
/// test on the wire rather than a source sweep alone -- the sweep pins the declaration, this
/// pins what a device receives.
///
/// The fixture fills the buffer with `(i * 7 + 1) &amp; 0xFF`, so a SHORT write and a write of
/// the WRONG bytes fail differently: the count assertion catches the truncation and the
/// content assertion catches a buffer read from the wrong base.
/// </summary>
[TestFixture]
public class CompatMpI2cBigBufferTests
{
    private const int Length = 300;

    private static string _hex = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _hex = PymcuCompiler.BuildFixture("compat-mp-i2c-big-buffer");

    // DISCRIMINATING. Before the widening this received 44 bytes, which is 300 truncated to
    // eight bits, with no diagnostic anywhere.
    [Test]
    public void Writeto_WithA300ByteBuffer_DeviceSeesAll300Bytes()
    {
        var uno = SimWithRecorder(0x48, out var recorder);
        uno.RunUntilSerial(uno.Serial, "READY\n", maxMs: 500);
        var after = uno.Serial.ByteCount;

        uno.Serial.InjectByte((byte)'W');
        uno.RunUntilSerialBytes(uno.Serial, after + 1, maxMs: 30000);

        recorder.ReceivedBytes.Count.Should().Be(Length,
            "writeto sends len(buf) bytes, and len(buf) may exceed 255");
    }

    // DISCRIMINATING in a second way: a count that is right says nothing about WHERE the
    // bytes were read from, so the position-dependent pattern is checked too.
    [Test]
    public void Writeto_WithA300ByteBuffer_SendsThePatternInOrder()
    {
        var uno = SimWithRecorder(0x48, out var recorder);
        uno.RunUntilSerial(uno.Serial, "READY\n", maxMs: 500);
        var after = uno.Serial.ByteCount;

        uno.Serial.InjectByte((byte)'W');
        uno.RunUntilSerialBytes(uno.Serial, after + 1, maxMs: 30000);

        var expected = Enumerable.Range(0, Length)
                                 .Select(i => (byte)((i * 7 + 1) & 0xFF))
                                 .ToArray();
        recorder.ReceivedBytes.Should().Equal(expected,
            "every byte comes from the buffer that was passed, in order");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ArduinoUnoSimulation SimWithRecorder(byte address,
        out RecordingI2cDevice recorder)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(_hex);
        uno.AddTwi(AvrTwi.TwiConfig, out var twi);
        var dev = new RecordingI2cDevice(twi, address);
        twi.EventHandler = dev;
        recorder = dev;
        return uno;
    }

    /// <summary>ACKs one address and records every byte written to it.</summary>
    private sealed class RecordingI2cDevice(AvrTwi twi, byte address) : ITwiEventHandler
    {
        public List<byte> ReceivedBytes { get; } = [];

        public void Start(bool repeated) => twi.CompleteStart();
        public void Stop() => twi.CompleteStop();

        public void ConnectToSlave(byte addr, bool write) =>
            twi.CompleteConnect(addr == address);

        public void WriteByte(byte data)
        {
            ReceivedBytes.Add(data);
            twi.CompleteWrite(true);
        }

        public void ReadByte(bool ack) => twi.CompleteRead(0xFF);
    }
}
