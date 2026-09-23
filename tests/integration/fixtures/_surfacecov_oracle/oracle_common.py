#!/usr/bin/env python3
# oracle_common.py -- shared fake-CircuitPython environment for the surfacecov
# fixtures. A fixture's oracle/oracle.py is three lines:
#
#   import os, sys
#   sys.path.insert(0, <repo>/tests/integration/fixtures/_surfacecov_oracle)
#   import oracle_common; oracle_common.run_here(__file__)
#
# It installs fake board/busio/digitalio/pwmio/pulseio/analogio/microcontroller/
# micropython/neopixel_write/supervisor modules, shims builtins.print to the
# format PyMCU's UART writers produce (float32 arithmetic, two decimals), then
# runs the fixture's src/main.py under stock CPython via runpy.
#
# Determinism contract with the sim runner (surfacerun / SurfaceCovTests):
#   fixture.json sits next to the fixture's oracle dir:
#     {"i2c":[addrs], "spi":true, "read_script":"readscript.txt",
#      "pins_low":[...], "pins_high":[...], "pulse_reply":[...]}
#   I2C/SPI read bytes come from readscript.txt consumed in order (0xFF when
#   empty); the sim's scripted slave consumes the same file the same way.
#   pulse_reply widths (us) feed fake PulseIn in trigger order.

import builtins
import json
import os
import runpy
import struct
import sys
import time as _time
import types
import typing


def _f32(v):
    """Round a Python float to the nearest float32, as PyMCU stores it."""
    return struct.unpack("f", struct.pack("f", v))[0]


def _u8(v):
    return int(v) & 0xFF


def pymcu_float_str(v):
    """The exact text uart_write_float(value:float) writes for a float32 v."""
    v = _f32(v)
    out = ""
    if v < 0.0:
        out += "-"
        v = -v
    int_part = int(v) & 0xFFFFFFFF          # uint32(value), truncating
    frac = _u8(_f32(_f32(v - _f32(float(int_part))) * 100.0 + 0.5))
    if frac >= 100:
        frac = 0
        int_part = (int_part + 1) & 0xFFFFFFFF
    tens = frac // 10
    frac = frac % 10
    s = f"{int_part}.{tens}"
    if frac != 0:
        s += f"{frac}"
    return out + s


def _py_print(*args, sep=" ", end="\n", file=None, flush=False):
    parts = []
    for a in args:
        if isinstance(a, bool):
            parts.append("True" if a else "False")
        elif isinstance(a, float):
            parts.append(pymcu_float_str(a))
        elif a is None:
            parts.append("None")
        else:
            parts.append(str(a))
    sys.stdout.write(sep.join(parts) + end)


class _Done(Exception):
    pass


# --------------------------------------------------------------------------
# Fake hardware
# --------------------------------------------------------------------------

class _Pin:
    """A board pin: just a name. Fake peripherals take the name as their key."""
    def __init__(self, name):
        self.name = name
    def __repr__(self):
        return f"Pin({self.name})"


class _PinValues:
    """Input levels from fixture.json; unknown pins read low."""
    def __init__(self, cfg):
        self._levels = {}
        for n in cfg.get("pins_low", []):
            self._levels[n] = False
        for n in cfg.get("pins_high", []):
            self._levels[n] = True
    def read(self, pin):
        return self._levels.get(getattr(pin, "name", pin), False)


class _Dir:
    INPUT = 0
    OUTPUT = 1
_DIR = _Dir()

class _Pull:
    UP = 1
    DOWN = 2

class _DM:
    PUSH_PULL = 0
    OPEN_DRAIN = 1


class FakeDigitalInOut:
    def __init__(self, pin):
        self._pin = pin
        self._direction = _DIR.INPUT
        self._pull = None
        self._drive_mode = _DM.PUSH_PULL
        self._out = False
    @property
    def direction(self):
        return self._direction
    @direction.setter
    def direction(self, d):
        self._direction = d
    @property
    def value(self):
        if self._direction == _DIR.OUTPUT:
            return self._out
        return _PINVALS.read(self._pin)
    @value.setter
    def value(self, v):
        self._out = bool(v)
    @property
    def pull(self):
        return self._pull
    @pull.setter
    def pull(self, p):
        self._pull = p
    @property
    def drive_mode(self):
        return self._drive_mode
    @drive_mode.setter
    def drive_mode(self, m):
        self._drive_mode = m
    def switch_to_output(self, value=False, drive_mode=_DM.PUSH_PULL):
        self._direction = _DIR.OUTPUT
        self._drive_mode = drive_mode
        self._out = bool(value)
    def switch_to_input(self, pull=None):
        self._direction = _DIR.INPUT
        self._pull = pull
    def deinit(self):
        self._direction = _DIR.INPUT
        self._pull = None
        self._drive_mode = _DM.PUSH_PULL
    def __enter__(self):
        return self
    def __exit__(self, *a):
        self.deinit()


class _ReadScript:
    """Shared read-byte source for both buses, consumed in order, 0xFF past end."""
    def __init__(self, path):
        self._bytes = []
        if path and os.path.exists(path):
            for tok in open(path).read().split():
                self._bytes.append(int(tok, 16))
        self._i = 0
    def take(self, n):
        out = bytearray()
        for _ in range(n):
            out.append(self._bytes[self._i] if self._i < len(self._bytes) else 0xFF)
            self._i += 1
        return bytes(out)


class FakeI2C:
    """busio.I2C stand-in: ACKs configured addresses, read bytes from script."""
    def __init__(self, scl=None, sda=None, *, frequency=400000, timeout=255):
        self._locked = False
        self.frequency = frequency
    def try_lock(self):
        if self._locked:
            return False
        self._locked = True
        return True
    def unlock(self):
        self._locked = False
    def _check(self, address):
        if _CFG.get("i2c") and address not in _CFG["i2c"]:
            raise OSError("[Errno 19] No such device")
    @staticmethod
    def _win(buf, start, end):
        if end is None:
            end = len(buf)
        return start, end
    def writeto(self, address, buffer, *, start=0, end=None):
        self._check(address)
        start, end = self._win(buffer, start, end)
        _ = bytes(buffer[start:end])
    def readfrom_into(self, address, buffer, *, start=0, end=None):
        self._check(address)
        start, end = self._win(buffer, start, end)
        buffer[start:end] = _SCRIPT.take(end - start)
    def writeto_then_readfrom(self, address, out_buffer, in_buffer, *,
                              out_start=0, out_end=None, in_start=0, in_end=None):
        self.writeto(address, out_buffer, start=out_start, end=out_end)
        self.readfrom_into(address, in_buffer, start=in_start, end=in_end)
    def scan(self):
        return sorted(_CFG.get("i2c", []))
    def deinit(self):
        pass
    def __enter__(self):
        return self
    def __exit__(self, *a):
        return False


class FakeSPI:
    # Real busio.SPI names the kwargs MOSI/MISO (uppercase); some callers pass
    # them positionally or as lowercase, so accept all spellings.
    def __init__(self, sck=None, mosi=None, miso=None, *,
                 clock=None, MOSI=None, MISO=None):
        self._locked = False
        self.frequency = None
    def try_lock(self):
        if self._locked:
            return False
        self._locked = True
        return True
    def unlock(self):
        self._locked = False
    def configure(self, *, baudrate=1000000, polarity=0, phase=0, bits=8):
        self.frequency = baudrate
    def write(self, buffer, *, start=0, end=None):
        s, e = FakeI2C._win(buffer, start, end)
        _ = bytes(buffer[s:e])
    def readinto(self, buffer, *, start=0, end=None, write_value=0):
        s, e = FakeI2C._win(buffer, start, end)
        buffer[s:e] = _SCRIPT.take(e - s)
    def write_readinto(self, out_buffer, in_buffer, *,
                       out_start=0, out_end=None, in_start=0, in_end=None):
        os_, oe = FakeI2C._win(out_buffer, out_start, out_end)
        is_, ie = FakeI2C._win(in_buffer, in_start, in_end)
        in_buffer[is_:ie] = _SCRIPT.take(ie - is_)
    def deinit(self):
        pass
    def __enter__(self):
        return self
    def __exit__(self, *a):
        return False


class FakePWMOut:
    """pwmio.PWMOut: stores duty/frequency; refuses out-of-range like CP."""
    def __init__(self, pin, *, duty_cycle=0, frequency=500, variable_frequency=False):
        self._pin = pin
        self._duty = duty_cycle
        self._freq = frequency
        self._varfreq = variable_frequency
    @property
    def duty_cycle(self):
        return self._duty
    @duty_cycle.setter
    def duty_cycle(self, v):
        if not 0 <= v <= 65535:
            raise ValueError("duty_cycle must be 0-65535")
        self._duty = v
    @property
    def frequency(self):
        return self._freq
    @frequency.setter
    def frequency(self, v):
        self._freq = v
    def deinit(self):
        pass
    def __enter__(self):
        return self
    def __exit__(self, *a):
        self.deinit()


class FakePulseIn:
    """pulseio.PulseIn fed by the fixture's pulse scripts.

    Two script shapes, both keyed by capture-pin name:

    * ``pulse_reply`` (per-trigger replies, HC-SR04 style): ``resume()``
      consumes one scripted width into the pending list, the next trigger's
      reply. A pin with an exhausted script captures nothing and the caller's
      timeout path runs, exactly like the firmware's.
    * ``pulse_frames`` (whole frames, DHT/IR style): the pin's scripted frames
      deliver in order -- the first at construction (the capture is armed from
      then on, and a real receiver has often already seen its burst by the
      time the program asks) and each next frame on ``resume()`` -- the call
      that re-arms a drained capture. ``clear()`` only empties the buffer: a
      ``clear(); resume()`` pair (adafruit_dht's measure preamble) delivers one
      frame, not two.

    The pending list survives ``pause()``: pausing stops new captures, it does
    not empty the buffer -- ``while self.pulse_in: popleft()`` after
    ``pause()`` is the drain idiom both callers use."""
    def __init__(self, pin, maxlen=2, idle_state=0):
        self._pin = pin
        self._maxlen = maxlen
        name = getattr(pin, "name", str(pin))
        frames = _CFG.get("pulse_frames", {})
        self._frames = [list(f) for f in frames.get(name, [])]
        queues = _CFG.get("pulse_queues", {})
        self._queue = queues.get(name, list(_CFG.get("pulse_widths_us", [])))
        self._pending = []
        self._paused = True
        # An armed capture that already has a scripted frame sees it land at
        # once; pulse_queues widths wait for resume() as before.
        if self._frames:
            self._pending = [((w * 2) & 0xFFFF) >> 1 for w in self._frames.pop(0)]
    def __len__(self):
        return len(self._pending)
    def __bool__(self):
        return len(self) > 0
    def __getitem__(self, i):
        return self._pending[i] if 0 <= i < len(self._pending) else 0
    def popleft(self):
        return self._pending.pop(0) if self._pending else 0
    @property
    def maxlen(self):
        return self._maxlen
    @property
    def paused(self):
        return 1 if self._paused else 0
    def clear(self):
        self._pending = []
    def pause(self):
        self._paused = True
    def resume(self, trigger_duration=0):
        # The AVR capture ISR timestamps edges off a free-running 16-bit timer
        # at 2 ticks/us and stores delta>>1, so a pulse longer than 32768 us
        # wraps -- 70000 us lands as ((140000)&0xffff)>>1 = 4464.
        if self._frames:
            self._pending = [((w * 2) & 0xFFFF) >> 1 for w in self._frames.pop(0)]
        elif self._queue:
            w = self._queue.pop(0)
            self._pending = [((w * 2) & 0xFFFF) >> 1]
        self._paused = False
    def deinit(self):
        pass
    def __enter__(self):
        return self
    def __exit__(self, *a):
        self.deinit()


def _ow_crc8(data):
    """1-Wire CRC-8 (maxim poly, reversed 0x8C) -- the same algorithm
    adafruit_onewire.crc8 and a real DS18B20 run."""
    crc = 0
    for byte in data:
        crc ^= byte
        for _ in range(8):
            crc = ((crc >> 1) ^ 0x8C) if (crc & 1) else (crc >> 1)
            crc &= 0xFF
    return crc


class _OneWireSlave:
    """A scripted 1-Wire slave, bit level, DS18B20 shape.

    Answers the protocol adafruit_onewire drives: reset presence, ROM command
    (MATCH 0x55 / SKIP 0xCC / SEARCH 0xF0 -- the search triple per position:
    slave serves bit then ~bit then reads the master's choice), then one
    function byte: CONVERT 0x44 (read slots answer 0x00 `convert_busy_reads`
    times, then 0xFF = done), READ_SCRATCH 0xBE (serves the nine scratchpad
    bytes LSB-first), WRITE_SCRATCH 0x4E (takes TH, TL, CONFIG and rewrites
    the CRC). fixture.json's "onewire" object keys one spec per pin name.
    """
    def __init__(self, spec):
        self.rom = bytes(spec["rom"])
        self.scratch = bytearray(spec["scratch"])
        self.busy = int(spec.get("convert_busy_reads", 0))
        self._reset()

    def _reset(self):
        self._wbuf = 0
        self._wbits = 0
        self._rqueue = []
        self._state = "cmd"     # cmd | match | func | wr3 | conv | idle
        self._match = []
        self._search_pos = 0
        self._search_pair = None
        self._wr = []
        self._conv = -1

    def reset(self):
        # Upstream reset() returns True when the bus stayed high (no device
        # pulled it low). A scripted slave always answers presence -> False.
        self._reset()
        return False

    def write_bit(self, b):
        b &= 1
        if self._state == "search":
            if self._search_pair:
                return          # write during the pair: malformed, ignore
            mybit = (self.rom[self._search_pos >> 3] >> (self._search_pos & 7)) & 1
            if b != mybit:
                self._state = "idle"     # a different device's turn; silent
            else:
                self._search_pos += 1
                if self._search_pos == 64:
                    self._state = "func"
            return
        self._wbuf |= b << self._wbits
        self._wbits += 1
        if self._wbits == 8:
            self._on_byte(self._wbuf)
            self._wbuf = 0
            self._wbits = 0

    def read_bit(self):
        if self._state == "search":
            if self._search_pair is None:
                bit = (self.rom[self._search_pos >> 3] >> (self._search_pos & 7)) & 1
                self._search_pair = [bit, 1 - bit]
            if self._search_pair:
                b = self._search_pair.pop(0)
                if not self._search_pair:
                    self._search_pair = None
                return b
            return 1
        if self._rqueue:
            return self._rqueue.pop(0)
        # A CONVERT leaves the device answering 0x00 while it works, then
        # 0xFF -- the shape `while buf[0] == 0: readinto(buf, end=1)` polls.
        if self._state == "func" and self._conv >= 0:
            byte = 0x00 if self._conv > 0 else 0xFF
            if self._conv > 0:
                self._conv -= 1
            for i in range(8):
                self._rqueue.append((byte >> i) & 1)
            return self._rqueue.pop(0)
        return 1    # nothing to say: the released bus reads high

    def _on_byte(self, byte):
        st = self._state
        if st == "cmd":
            if byte == 0x55:            # MATCH_ROM
                self._state = "match"
                self._match = []
            elif byte == 0xF0:          # SEARCH_ROM
                self._state = "search"
                self._search_pos = 0
                self._search_pair = None
            elif byte == 0xCC:          # SKIP_ROM
                self._state = "func"
            else:
                self._state = "idle"
        elif st == "match":
            self._match.append(byte)
            if len(self._match) == 8:
                self._state = "func" if bytes(self._match) == self.rom else "idle"
        elif st == "func":
            # Once ROM-matched the device stays addressed until the next
            # reset, so function bytes keep arriving back-to-back.
            if byte == 0x44:            # CONVERT
                self._conv = self.busy
            elif byte == 0xBE:          # READ_SCRATCH
                self._conv = -1
                for bb in self.scratch:
                    for i in range(8):
                        self._rqueue.append((bb >> i) & 1)
            elif byte == 0x4E:          # WRITE_SCRATCH: TH, TL, CONFIG
                self._state = "wr3"
                self._wr = []
        elif st == "wr3":
            self._wr.append(byte)
            if len(self._wr) == 3:
                self.scratch[2] = self._wr[0]
                self.scratch[3] = self._wr[1]
                self.scratch[4] = self._wr[2]
                self.scratch[8] = _ow_crc8(self.scratch[:8])
                self._state = "func"


class FakeOneWire:
    """onewireio.OneWire: bit-level access to the pin's scripted slave. A pin
    with no "onewire" entry is an empty bus -- reset() reports nobody."""
    def __init__(self, pin):
        name = getattr(pin, "name", str(pin))
        spec = _CFG.get("onewire", {}).get(name)
        self._slave = _OneWireSlave(spec) if spec else None
    def reset(self):
        if self._slave is None:
            return True
        return self._slave.reset()
    def read_bit(self):
        return 1 if self._slave is None else self._slave.read_bit()
    def write_bit(self, value):
        if self._slave is not None:
            self._slave.write_bit(value)


class FakePulseOut:
    def __init__(self, pin, frequency=38000, duty_cycle=32768):
        pass
    def send(self, pulses):
        pass
    def deinit(self):
        pass
    def __enter__(self):
        return self
    def __exit__(self, *a):
        self.deinit()


class FakeAnalogIn:
    def __init__(self, pin):
        self._pin = pin
    @property
    def value(self):
        return _CFG.get("adc_value", 32768)
    @property
    def reference_voltage(self):
        return _f32(_CFG.get("adc_ref", 3.3))
    def deinit(self):
        pass
    def __enter__(self):
        return self
    def __exit__(self, *a):
        self.deinit()


class FakeAnalogOut:
    def __init__(self, pin):
        self.value = 0
    def deinit(self):
        pass
    def __enter__(self):
        return self
    def __exit__(self, *a):
        self.deinit()


class FakeUART:
    def __init__(self, tx=None, rx=None, *, baudrate=9600, **kw):
        self.baudrate = baudrate
    def write(self, buf):
        return len(buf)
    def read(self, n):
        return None
    def readinto(self, buf):
        return 0
    @property
    def in_waiting(self):
        return 0
    def deinit(self):
        pass
    def __enter__(self):
        return self
    def __exit__(self, *a):
        return False


class FakeNeoPixelWrite:
    pass


def _install(cfg, scriptdir):
    global _CFG, _SCRIPT, _PINVALS
    _CFG = cfg
    _SCRIPT = _ReadScript(os.path.join(scriptdir, cfg.get("read_script", "readscript.txt")))
    _PINVALS = _PinValues(cfg)

    pins = {}
    def _mkpin(n):
        if n not in pins:
            pins[n] = _Pin(n)
        return pins[n]

    board = types.ModuleType("board")
    for i in range(14):
        setattr(board, f"D{i}", _mkpin(f"D{i}"))
    for i in range(6):
        setattr(board, f"A{i}", _mkpin(f"A{i}"))
    board.LED = _mkpin("D13")
    board.SCL = _mkpin("A5")
    board.SDA = _mkpin("A4")
    board.SCK = _mkpin("D13")
    board.MOSI = _mkpin("D11")
    board.MISO = _mkpin("D12")
    board.RX = _mkpin("D0")
    board.TX = _mkpin("D1")
    _i2c = FakeI2C()
    _spi = FakeSPI()
    board.I2C = lambda: _i2c
    board.SPI = lambda: _spi
    board.UART = lambda: FakeUART()
    sys.modules["board"] = board

    busio = types.ModuleType("busio")
    busio.I2C = FakeI2C
    busio.SPI = FakeSPI
    busio.UART = FakeUART
    sys.modules["busio"] = busio

    bitbangio = types.ModuleType("bitbangio")
    bitbangio.I2C = FakeI2C
    bitbangio.SPI = FakeSPI
    sys.modules["bitbangio"] = bitbangio

    digitalio = types.ModuleType("digitalio")
    digitalio.DigitalInOut = FakeDigitalInOut
    digitalio.Direction = _DIR
    digitalio.Pull = _Pull
    digitalio.DriveMode = _DM
    sys.modules["digitalio"] = digitalio

    pwmio = types.ModuleType("pwmio")
    pwmio.PWMOut = FakePWMOut
    sys.modules["pwmio"] = pwmio

    pulseio = types.ModuleType("pulseio")
    pulseio.PulseIn = FakePulseIn
    pulseio.PulseOut = FakePulseOut
    sys.modules["pulseio"] = pulseio

    onewireio = types.ModuleType("onewireio")
    onewireio.OneWire = FakeOneWire
    sys.modules["onewireio"] = onewireio

    analogio = types.ModuleType("analogio")
    analogio.AnalogIn = FakeAnalogIn
    analogio.AnalogOut = FakeAnalogOut
    sys.modules["analogio"] = analogio

    microcontroller = types.ModuleType("microcontroller")
    microcontroller.Pin = _Pin
    microcontroller.cpu = types.SimpleNamespace(frequency=160000000)
    microcontroller.delay_us = lambda us: None
    sys.modules["microcontroller"] = microcontroller

    micropython = types.ModuleType("micropython")
    micropython.const = lambda x: x
    sys.modules["micropython"] = micropython

    npw = types.ModuleType("neopixel_write")
    npw.neopixel_write = lambda pin, buf: None
    sys.modules["neopixel_write"] = npw

    supervisor = types.ModuleType("supervisor")
    _tick = [0]
    def _ticks_ms():
        # A real millis() reads the same value every call within the same ms --
        # it advances with time, not per read. time.sleep() below is what moves
        # it, matching the simulator where delay_ms burns real clock ticks.
        return _tick[0] % (1 << 29)
    supervisor.ticks_ms = _ticks_ms
    sys.modules["supervisor"] = supervisor

    rainbowio = types.ModuleType("rainbowio")
    def _colorwheel(pos):
        pos = pos & 255
        if pos < 85:
            return (255 - pos * 3) << 16 | (pos * 3) << 8 | 0
        if pos < 170:
            pos -= 85
            return 0 << 16 | (255 - pos * 3) << 8 | pos * 3
        pos -= 170
        return (pos * 3) << 16 | 0 << 8 | (255 - pos * 3)
    rainbowio.colorwheel = _colorwheel
    sys.modules["rainbowio"] = rainbowio

    cpt = types.ModuleType("circuitpython_typing")
    cpt.ReadableBuffer = typing.Union[bytes, bytearray, memoryview]
    cpt.WriteableBuffer = typing.Union[bytearray, memoryview]
    sys.modules["circuitpython_typing"] = cpt
    cpt_io = types.ModuleType("circuitpython_typing.io")
    class _ROValueIO:
        @property
        def value(self):
            return 0.0
    cpt_io.ROValueIO = _ROValueIO
    sys.modules["circuitpython_typing.io"] = cpt_io

    # time.sleep burns the fake clock the same way delay_ms burns the real one,
    # so a program that spaces calls by sleeping sees the ticks it slept past.
    def _sleep(seconds=0):
        _tick[0] += int(seconds * 1000)
    _time.sleep = _sleep


def run_here(oracle_file):
    """Entry point for a fixture oracle: <fixture>/oracle/oracle.py."""
    fixture = os.path.dirname(os.path.dirname(os.path.abspath(oracle_file)))
    cfgpath = os.path.join(fixture, "fixture.json")
    cfg = {}
    if os.path.exists(cfgpath):
        cfg = json.load(open(cfgpath))
    # i2c addresses may be written "0x49" in json
    if "i2c" in cfg:
        cfg["i2c"] = [int(a, 16) if isinstance(a, str) else a for a in cfg["i2c"]]
    # pulse_reply widths feed FakePulseIn, queued per ECHO pin (the sim fires a
    # reply only when its own trigger pin falls); an explicit pulse_widths_us
    # list remains the flat fallback for pins with no entry.
    if "pulse_reply" in cfg:
        queues = {}
        for r in cfg["pulse_reply"]:
            n = r.get("count", -1)
            queues.setdefault(r["echo"], []).extend(
                [r["width_us"]] * (n if n > 0 else 1))
        cfg["pulse_queues"] = queues
        cfg["pulse_widths_us"] = [r["width_us"] for r in cfg["pulse_reply"]
                                  for _ in range(r.get("count", -1) if r.get("count", -1) > 0 else 1)]
    # read_script resolves against the fixture root, matching the sim side
    # (surfacerun joins it to the fixture.json directory).
    _install(cfg, fixture)

    builtins.print = _py_print

    src = os.path.join(fixture, "src")
    sys.path.insert(0, src)
    try:
        runpy.run_path(os.path.join(src, "main.py"), run_name="__main__")
    except _Done:
        pass
    except SystemExit:
        pass
    except BaseException as e:
        # Mirror the firmware's unhandled-exception line so a program that
        # dies the same death on both sides still compares equal.
        print(f"E:{type(e).__name__}: {e}")
