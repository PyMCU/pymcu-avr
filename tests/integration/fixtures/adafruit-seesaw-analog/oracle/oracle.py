#!/usr/bin/env python3
# CPython oracle for the adafruit-seesaw-analog fixture.
#
# Runs the byte-identical upstream library files in ../src against a fake I2C
# bus under stock CPython and prints every I2C transaction the program emits,
# one per line, as:
#
#   <addr hex> <data hex>        write transaction (data may be empty)
#   <addr hex> R <data hex>      read transaction
#
# The transaction stream is the reference the AVR integration test compares the
# emulated firmware's TWI traffic against. Nothing here is hand-derived expected
# data: the bytes come out of the same adafruit_seesaw sources the compiler is
# given.
#
# Read data is scripted so the constructor takes a real chip path: the HW_ID
# byte answers 0x55 (SAMD09), the 4-byte VERSION read answers 0x00000001 (a
# product id that matches none of the Crickit/RoboHAT/569x cases, so the
# pin_mapping lands on SAMD09_Pinmap). analog_read(2) then finds 2 in
# SAMD09_Pinmap.analog_pins, takes offset = index(2) = 0, and issues a
# write of [0x09, 0x07] followed by a 2-byte read. The same answers are what
# the emulated bus gives the firmware, so the two transaction streams diverge
# only when the compiler emits different bytes.
#
# The program's `while True` never returns on its own, so the fake bus raises
# _Done after TXN_CAP transactions; the stream up to that point is the
# reference. The program's own print() (the analog value) goes to stderr so the
# oracle's stdout stays a clean transaction list.
#
# Usage: .venv/bin/python oracle.py   (cwd does not matter; paths are __file__-relative)

import contextlib
import os
import runpy
import sys
import types
import typing

SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src")
SRC = os.path.normpath(SRC)

# (address, is_write, payload) in wire order.
TRANSACTIONS = []

# Each loop iteration is init (probe, sw_reset, chip-id write+read, version
# write+read = 6) plus analog_read's write+read (2). 16 is two full
# iterations -- enough to compare setup and the repeating read.
TXN_CAP = 16


class _Done(BaseException):
    """Raised out of the bus once enough transactions have been recorded.

    BaseException, not Exception: the program under test wraps the constructor
    and the read in `except Exception`, which would swallow a plain Exception
    and loop forever.
    """


class FakeI2C:
    """Stands in for CircuitPython busio.I2C.

    Mirrors what CircuitPython's busio.I2C does with the buffer window
    arguments: writeto sends buffer[start:end] (end defaults to len(buffer)),
    readfrom_into fills buffer[start:end]. The I2CDevice probe writes b"",
    which lands here as a zero-length write transaction -- on the wire that is
    START, SLA+W, STOP with no data byte, and it is recorded as such.
    """

    def __init__(self, scl=None, sda=None, *, frequency=400000, timeout=255):
        self._locked = False

    def try_lock(self):
        if self._locked:
            return False
        self._locked = True
        return True

    def unlock(self):
        self._locked = False

    @staticmethod
    def _window(buffer, start, end):
        if end is None:
            end = len(buffer)
        return bytes(buffer[start:end])

    def _record(self, address, is_write, data):
        TRANSACTIONS.append((address, is_write, data))
        if len(TRANSACTIONS) >= TXN_CAP:
            raise _Done()

    def writeto(self, address, buffer, *, start=0, end=None):
        self._record(address, True, self._window(buffer, start, end))

    def readfrom_into(self, address, buffer, *, start=0, end=None):
        if end is None:
            end = len(buffer)
        n = end - start
        # Scripted answers: 1-byte reads are the HW_ID probe (0x55 = SAMD09);
        # 4-byte reads are VERSION/OPTIONS words whose top 16 bits are the pid
        # (0x0000 matches none of the named products). The 2-byte ADC read --
        # and anything else -- gets 0xFF bytes.
        if n == 1:
            data = bytes([0x55])
        elif n == 4:
            data = bytes([0x00, 0x00, 0x00, 0x01])
        else:
            data = bytes([0xFF] * n)
        buffer[start:end] = data
        self._record(address, False, data)

    def writeto_then_readfrom(self, address, out_buffer, in_buffer, *,
                              out_start=0, out_end=None,
                              in_start=0, in_end=None):
        # On the wire: write phase, repeated START, read phase, one STOP. The
        # recorder closes a transaction at a repeated start, so this lands as a
        # write transaction followed by a read transaction.
        self.writeto(address, out_buffer, start=out_start, end=out_end)
        self.readfrom_into(address, in_buffer, start=in_start, end=in_end)

    def scan(self):
        return sorted({addr for addr, _, _ in TRANSACTIONS})

    def deinit(self):
        pass

    def __enter__(self):
        return self

    def __exit__(self, *args):
        self.deinit()
        return False


class FakeSPI:
    """Annotation target only (busio.SPI / SPIDevice signatures); never built."""

    def __init__(self, *args, **kwargs):
        raise NotImplementedError("SPI is not exercised by this program")


class FakeDigitalInOut:
    """Annotation target only (digitalio.DigitalInOut); never instantiated."""

    def __init__(self, *args, **kwargs):
        raise NotImplementedError("digitalio is not exercised by this program")


def _install_fake_modules():
    bus = FakeI2C()

    board = types.ModuleType("board")
    board.SCL = "PC5"
    board.SDA = "PC4"
    board.I2C = lambda: bus
    sys.modules["board"] = board

    busio = types.ModuleType("busio")
    busio.I2C = FakeI2C
    busio.SPI = FakeSPI
    sys.modules["busio"] = busio

    digitalio = types.ModuleType("digitalio")
    digitalio.DigitalInOut = FakeDigitalInOut
    sys.modules["digitalio"] = digitalio

    micropython = types.ModuleType("micropython")
    micropython.const = lambda expr: expr
    sys.modules["micropython"] = micropython

    # The program sleeps half a second per iteration; under the oracle a sleep
    # is a no-op. time is a real module, so patch the attribute instead of the
    # module table.
    import time as _time
    _time.sleep = lambda *_a, **_kw: None

    # i2c_device.py binds ReadableBuffer/WriteableBuffer for its method
    # annotations; without the package the try/except leaves them undefined and
    # class creation raises NameError on CPython.
    cpt = types.ModuleType("circuitpython_typing")
    cpt.ReadableBuffer = typing.Union[bytes, bytearray, memoryview]
    cpt.WriteableBuffer = typing.Union[bytearray, memoryview]
    sys.modules["circuitpython_typing"] = cpt


def main():
    _install_fake_modules()
    sys.path.insert(0, SRC)
    # The program prints the analog value it read; keep stdout a pure
    # transaction list by sending the program's prints to stderr.
    with contextlib.redirect_stdout(sys.stderr):
        try:
            runpy.run_path(os.path.join(SRC, "main.py"), run_name="__main__")
        except _Done:
            pass

    for address, is_write, data in TRANSACTIONS:
        if is_write:
            print(f"{address:02X} {data.hex()}")
        else:
            print(f"{address:02X} R {data.hex()}")


if __name__ == "__main__":
    main()
