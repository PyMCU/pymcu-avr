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
    def __init__(self, sck=None, mosi=None, miso=None):
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
    """pulseio.PulseIn fed by the fixture's pulse_reply width list."""
    def __init__(self, pin, maxlen=2, idle_state=0):
        self._pin = pin
        self._maxlen = maxlen
        self._queue = list(_CFG.get("pulse_widths_us", []))
        self._paused = True
    def __len__(self):
        return 1 if (self._queue and not self._paused) else 0
    def __bool__(self):
        return len(self) > 0
    def __getitem__(self, i):
        if not self._queue:
            return 0
        return self._queue[0] if i == 0 else 0
    def popleft(self):
        return self._queue.pop(0) if self._queue else 0
    @property
    def maxlen(self):
        return self._maxlen
    @property
    def paused(self):
        return 1 if self._paused else 0
    def clear(self):
        pass
    def pause(self):
        self._paused = True
    def resume(self, trigger_duration=0):
        self._paused = False
    def deinit(self):
        pass
    def __enter__(self):
        return self
    def __exit__(self, *a):
        self.deinit()


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
        _tick[0] = (_tick[0] + 1) % (1 << 29)
        return _tick[0]
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

    # time.sleep burns only the budget, never the answer.
    _time.sleep = lambda *_a, **_kw: None


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
    # pulse_reply widths feed FakePulseIn in order
    if "pulse_reply" in cfg:
        cfg["pulse_widths_us"] = [r["width_us"] for r in cfg["pulse_reply"]
                                  for _ in range(r.get("count", -1) if r.get("count", -1) > 0 else 1)]
    _install(cfg, os.path.join(fixture, "oracle"))

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
