using PyMCU.TestKit;
using RP2040.Peripherals;

namespace PyMCU.OracleCircuitPython;

/// <summary>
/// Records every I2C transaction targeting the SSD1306 at 0x3C, on both bus
/// paths CircuitPython uses:
///  - the DW_apb_i2c peripheral (I2cPeripheral OnWrite/OnRead/OnStop hooks) for
///    len&gt;0 transfers;
///  - the bitbang engine on GP0(SDA)/GP1(SCL) for zero-length writes -- the
///    RP2040 I2C peripheral cannot emit a bare address probe, so
///    common_hal_busio_i2c_write falls back to bitbangio for len == 0
///    (ports/raspberrypi/common-hal/busio/I2C.c). Those bytes are decoded from
///    SIO pad edges, and the slave ACK is driven by forcing the SDA pad low
///    through GpioPin.ForceInput.
/// </summary>
internal sealed class Ssd1306WireRecorder
{
    public List<I2cTransaction> Transactions { get; } = new();
    public int HwCount { get; private set; }
    public int BitbangCount { get; private set; }

    private const byte OledAddr = 0x3C;
    private const int SdaPin = 0; // GP0 -- busio.I2C(board.GP1, board.GP0): SCL=GP1, SDA=GP0
    private const int SclPin = 1; // GP1

    private readonly RP2040Machine _m;

    private bool _hwOpen;
    private byte _hwAddr;
    private bool _hwWrite;
    private readonly List<byte> _hwData = new();

    private bool _slaveSdaLow;
    private bool _sdaWire = true, _sclWire = true;
    private bool _inFrame;
    private int _bitIdx;
    private int _cur;
    private int _byteCount;
    private bool _sawAddr;
    private byte _segAddr;
    private bool _segWrite;
    private readonly List<byte> _segData = new();
    private readonly Action<uint>? _prevHandler;

    public Ssd1306WireRecorder(RP2040Machine m)
    {
        _m = m;
        foreach (var i2c in new[] { m.I2c0, m.I2c1 })
        {
            i2c.DeviceResponds = a => a == OledAddr;
            i2c.OnWrite += HwWrite;
            i2c.OnRead += HwRead;
            i2c.OnStop += HwStop;
        }

        _prevHandler = m.Sio.OnGpioChanged;
        m.Sio.OnGpioChanged = mask =>
        {
            _prevHandler?.Invoke(mask);
            if ((mask & 0b11u) != 0) OnPadsChanged();
        };
        PublishWire();
    }

    public void Clear()
    {
        Transactions.Clear();
        HwCount = 0;
        BitbangCount = 0;
    }

    // ── hardware I2C peripheral callbacks ────────────────────────────────

    private void HwWrite(byte addr, byte data)
    {
        if (_hwOpen && (_hwAddr != addr || !_hwWrite)) CommitHw();
        if (!_hwOpen) { _hwOpen = true; _hwAddr = addr; _hwWrite = true; _hwData.Clear(); }
        _hwData.Add(data);
    }

    private byte HwRead(byte addr)
    {
        if (_hwOpen && (_hwAddr != addr || _hwWrite)) CommitHw();
        if (!_hwOpen) { _hwOpen = true; _hwAddr = addr; _hwWrite = false; _hwData.Clear(); }
        _hwData.Add(0xFF);
        return 0xFF;
    }

    private void HwStop() { if (_hwOpen) CommitHw(); }

    private void CommitHw()
    {
        HwCount++;
        Transactions.Add(new I2cTransaction(_hwAddr, _hwWrite, _hwData.ToArray()));
        _hwOpen = false;
        _hwData.Clear();
    }

    // ── bitbang wire model + I2C slave decoder ───────────────────────────

    private bool MasterLow(int pin) =>
        _m.IoBank0.GetPadOutputEnable(pin) && !_m.IoBank0.GetPadOutputLevel(pin);

    private void PublishWire()
    {
        _m.Gpio[SdaPin].ForceInput(!_slaveSdaLow);
        _m.Gpio[SclPin].ForceInput(true);
    }

    private void OnPadsChanged()
    {
        var newSda = !MasterLow(SdaPin) && !_slaveSdaLow;
        var newScl = !MasterLow(SclPin);
        var fallSda = _sdaWire && !newSda;
        var riseSda = !_sdaWire && newSda;
        var riseScl = !_sclWire && newScl;
        var fallScl = _sclWire && !newScl;
        _sdaWire = newSda;
        _sclWire = newScl;

        if (riseSda && newScl)                      // STOP
        {
            if (_inFrame) CommitSegment();
            _inFrame = false;
        }
        if (fallSda && newScl)                      // (repeated) START
        {
            if (_inFrame && _sawAddr) CommitSegment();
            _inFrame = true;
            _bitIdx = 0; _cur = 0; _byteCount = 0;
            _sawAddr = false; _segData.Clear();
            _slaveSdaLow = false;
        }

        if (_inFrame)
        {
            if (riseScl)
            {
                _bitIdx++;
                if (_bitIdx <= 8)
                {
                    _cur = (_cur << 1) | (newSda ? 1 : 0);
                    if (_bitIdx == 8) ByteClocked((byte)_cur);
                }
            }
            else if (fallScl)
            {
                if (_bitIdx == 8)
                {
                    // ACK slot: pull SDA low when the frame targets the OLED --
                    // address ACK (byte 1) and every byte of a write.
                    _slaveSdaLow = _sawAddr && _segAddr == OledAddr &&
                                   (_byteCount == 1 || _segWrite);
                }
                else if (_bitIdx == 9)
                {
                    _bitIdx = 0; _cur = 0;
                    _slaveSdaLow = false;
                }
            }
        }
        PublishWire();
    }

    private void ByteClocked(byte b)
    {
        if (_byteCount == 0)
        {
            _segAddr = (byte)(b >> 1);
            _segWrite = (b & 1) == 0;
            _sawAddr = true;
        }
        else if (_segWrite) _segData.Add(b);
        else _segData.Add(0xFF);
        _byteCount++;
    }

    private void CommitSegment()
    {
        if (_sawAddr)
        {
            BitbangCount++;
            Transactions.Add(new I2cTransaction(_segAddr, _segWrite, _segData.ToArray()));
        }
        _inFrame = false;
        _sawAddr = false;
        _segData.Clear();
        _byteCount = 0;
        _slaveSdaLow = false;
    }
}
