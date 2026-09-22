using Avr8Sharp.TestKit;
using AVR8Sharp.Core.Peripherals;

namespace PyMCU.TestKit;

/// <summary>
/// The recorded stream plus, when the run stopped abnormally, a description
/// of the stop (exception, PC, SP) -- a firmware crash before or mid-stream
/// is a divergence too, and it must be reported, not thrown through the runner.
/// </summary>
public sealed record WireTrace(List<I2cTransaction> Transactions, string? Crash);

/// <summary>
/// ACKs <paramref name="address"/> only; records transactions with boundaries.
/// With <paramref name="nackAll"/> every connect is NACKed instead -- the dead-bus
/// shape behind the SSD1306 field report, where no address ever answered.
/// </summary>
public sealed class TwiRecorder(AvrTwi twi, byte address, bool nackAll = false) : ITwiEventHandler
{
    private readonly List<byte> _current = [];
    private byte _addr;
    private bool _write;
    private bool _open;

    /// <summary>
    /// Bytes the slave answers a read with, consumed in order. When the script
    /// runs out the byte is 0xFF -- the same answer an unpadded responder gives.
    /// The recorded transaction always holds the bytes the master actually read.
    /// </summary>
    public Queue<byte> ReadBytes { get; } = new();

    public List<I2cTransaction> Transactions { get; } = [];

    public void Start(bool repeated) => twi.CompleteStart();

    public void Stop()
    {
        Close();
        twi.CompleteStop();
    }

    public void ConnectToSlave(byte addr, bool write)
    {
        Close();  // a repeated START closes the previous transaction
        _addr = addr;
        _write = write;
        _current.Clear();
        _open = true;
        twi.CompleteConnect(!nackAll && addr == address);
    }

    public void WriteByte(byte data)
    {
        _current.Add(data);
        twi.CompleteWrite(true);
    }

    public void ReadByte(bool ack)
    {
        var data = ReadBytes.Count > 0 ? ReadBytes.Dequeue() : (byte)0xFF;
        _current.Add(data);
        twi.CompleteRead(data);
    }

    /// <summary>Closes a transaction still open when the run stops (no STOP seen).</summary>
    public void Flush() => Close();

    private void Close()
    {
        if (!_open) return;
        Transactions.Add(new I2cTransaction(_addr, _write, _current.ToArray()));
        _open = false;
    }
}
