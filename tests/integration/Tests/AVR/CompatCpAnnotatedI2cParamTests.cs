using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;
using AVR8Sharp.Core.Peripherals;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/compat-cp-annotated-i2c-param: a method call on
/// a parameter annotated with an imported class, <c>def probe(b: busio.I2C)</c>,
/// on an instance built by <c>board.I2C()</c>.
///
/// board.I2C is a plain function (<c>def I2C(): return _board_i2c(SCL, SDA)</c>),
/// not the constructor itself, and nothing declared its return type. The
/// assignment never learned the class, so <c>b.try_lock()</c> flattened the
/// receiver's own name and the build asked for the undefined 'i2c_try_lock'.
/// The same call through a field (<c>self.i2c.try_lock()</c>, the adafruit
/// I2CDevice shape) already resolved, which is why the libraries worked and the
/// direct shape did not.
///
/// On the wire the probe is exactly one empty write: START, SLA+W for 0x3C,
/// STOP, no data bytes -- try_lock() is a flag, not a transaction.
/// </summary>
[TestFixture]
public class CompatCpAnnotatedI2cParamTests
{
    private const byte OledAddr = 0x3C;

    private static string _hex = null!;
    private static string _hexPy = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _hex   = PymcuCompiler.BuildFixture("compat-cp-annotated-i2c-param");
        _hexPy = PymcuCompiler.BuildFixturePyParser("compat-cp-annotated-i2c-param");
    }

    /// <summary>Boots, records I2C traffic to 0x3C, runs until "OK" (probe done).</summary>
    private static ProbeI2cDevice Run(string hex)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        // The bus sits idle-high on the bench; the GPIO model drives PIN only
        // from injected values, and CircuitPython's busio.I2C refuses a line
        // that reads low at construction.
        uno.PortC.SetPinValue(4, true);   // SDA
        uno.PortC.SetPinValue(5, true);   // SCL
        uno.AddTwi(AvrTwi.TwiConfig, out var twi);
        var recorder = new ProbeI2cDevice(twi, OledAddr);
        twi.EventHandler = recorder;

        uno.RunUntilSerial(uno.Serial, "OK\n", maxMs: 2000);
        return recorder;
    }

    [Test]
    public void Probe_EmitsTheEmptyWriteTo0x3C()
    {
        var recorder = Run(_hex);
        recorder.SlaveWrites.Should().Be(1,
            "writeto(0x3C, b\"\") is exactly one connection to the slave");
        recorder.ReceivedBytes.Should().BeEmpty(
            "an empty buffer writes the address byte and nothing else");
    }

    [Test]
    public void Probe_TheSameThroughThePythonFrontEnd()
    {
        var recorder = Run(_hexPy);
        recorder.SlaveWrites.Should().Be(1,
            "PYMCU_PY_PARSER=1 must dispatch b.try_lock() the same way");
        recorder.ReceivedBytes.Should().BeEmpty();
    }

    /// <summary>ACKs 0x3C, counts slave connections, records any data bytes.</summary>
    private sealed class ProbeI2cDevice(AvrTwi twi, byte address) : ITwiEventHandler
    {
        public int SlaveWrites { get; private set; }
        public List<byte> ReceivedBytes { get; } = [];

        public void Start(bool repeated) => twi.CompleteStart();
        public void Stop() => twi.CompleteStop();

        public void ConnectToSlave(byte addr, bool write)
        {
            if (addr == address && write) SlaveWrites++;
            twi.CompleteConnect(addr == address);
        }

        public void WriteByte(byte data)
        {
            ReceivedBytes.Add(data);
            twi.CompleteWrite(true);
        }

        public void ReadByte(bool ack) => twi.CompleteRead(0xFF);
    }
}
