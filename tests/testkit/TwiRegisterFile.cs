using Avr8Sharp.TestKit;
using AVR8Sharp.Core.Peripherals;

namespace PyMCU.TestKit;

/// <summary>
/// A TWI recorder that is also a register file: <paramref name="address"/> is
/// ACKed, the first byte of a write transaction latches the register pointer,
/// further write bytes store at auto-incrementing addresses, and each read
/// byte answers <c>registers[ptr]</c> then advances it. The stream records the
/// bytes actually served, so a read divergence shows up as stream data -- the
/// same contract <see cref="TwiRecorder"/> gives for writes.
///
/// Built for sensors whose drivers validate a chip id or calibration block at
/// construction (adafruit-bmp280-unmodified needs 0x58 at 0xD0 and the 24-byte
/// DIG_* block at 0x88); an ACK-and-0xFF slave would fail the driver's
/// chip-id check before the interesting traffic starts.
/// </summary>
public sealed class TwiRegisterFile(AvrTwi twi, byte address, byte[] registers) : ITwiRecorder
{
    private readonly List<byte> _current = [];
    private byte _addr;
    private bool _write;
    private bool _open;
    private int _ptr;
    private int _txnBytes;

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
        _txnBytes = 0;
        _open = true;
        twi.CompleteConnect(addr == address);
    }

    public void WriteByte(byte data)
    {
        _current.Add(data);
        if (_txnBytes == 0)
            _ptr = data;
        else
        {
            registers[_ptr & 0xFF] = data;
            _ptr = (_ptr + 1) & 0xFF;
        }
        _txnBytes++;
        twi.CompleteWrite(true);
    }

    public void ReadByte(bool ack)
    {
        var b = registers[_ptr & 0xFF];
        _ptr = (_ptr + 1) & 0xFF;
        _current.Add(b);
        twi.CompleteRead(b);
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
