using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;
using AVR8Sharp.Core.Peripherals;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for the SSD1306 OLED driver init sequence (examples/ssd1306).
///
/// The driver drives its 25-command init from a flash-resident const[uint8[25]]
/// table walked by a runtime loop (pymcu.drivers._ssd1306.i2c._ssd1306_init),
/// instead of 25 inlined sends. These tests pin the observable behaviour: every
/// byte the table emits over I2C, in order, so the table/loop can never silently
/// drift from the datasheet sequence.
///
/// Wire protocol per command (_ssd1306_cmd): START, SLA+W, 0x00 (control =
/// command stream), cmd, STOP. SLA+W is consumed by ConnectToSlave, so the
/// recorder sees the pair [0x00, cmd] for each of the 25 commands.
/// </summary>
[TestFixture]
public class Ssd1306InitTests
{
    // The datasheet 128x64 init sequence the flash table must reproduce.
    private static readonly byte[] InitSequence =
    {
        0xAE, 0xD5, 0x80, 0xA8, 0x3F, 0xD3, 0x00, 0x40, 0x8D, 0x14,
        0x20, 0x00, 0xA1, 0xC8, 0xDA, 0x12, 0x81, 0xCF, 0xD9, 0xF1,
        0xDB, 0x40, 0xA4, 0xA6, 0xAF,
    };

    private const byte OledAddr = 0x3C;

    private static string _hex = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _hex = PymcuCompiler.Build("ssd1306");

    /// <summary>Boots, records I2C traffic to 0x3C, runs until "OK" (init done).</summary>
    private static RecordingI2cDevice RunInit()
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(_hex);
        uno.AddTwi(AvrTwi.TwiConfig, out var twi);
        var recorder = new RecordingI2cDevice(twi, OledAddr);
        twi.EventHandler = recorder;

        // main: println("OLED"), oled.init(), println("OK"). "OK" => init complete.
        uno.RunUntilSerial(uno.Serial, "OLED\nOK\n", maxMs: 2000);
        return recorder;
    }

    [Test]
    public void Init_SendsExactly25Commands()
    {
        var recorder = RunInit();
        // Each command is a [control, cmd] pair on the wire.
        recorder.ReceivedBytes.Count.Should().Be(InitSequence.Length * 2,
            "25 init commands, each a control byte + command byte");
    }

    [Test]
    public void Init_EveryControlByteIsCommandStream()
    {
        var recorder = RunInit();
        for (int i = 0; i < recorder.ReceivedBytes.Count; i += 2)
            recorder.ReceivedBytes[i].Should().Be(0x00,
                $"control byte {i / 2} selects the command stream");
    }

    [Test]
    public void Init_EmitsDatasheetSequenceInOrder()
    {
        var recorder = RunInit();
        // Command bytes are the odd indices; they must match the table exactly.
        var commands = new List<byte>();
        for (int i = 1; i < recorder.ReceivedBytes.Count; i += 2)
            commands.Add(recorder.ReceivedBytes[i]);

        commands.Should().Equal(InitSequence,
            "the flash table + loop must reproduce the full init sequence in order");
    }

    /// <summary>ACKs a specific address and records bytes written to it.</summary>
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
