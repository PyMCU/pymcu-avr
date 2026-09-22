#!/usr/bin/env python3
# CPython oracle for the adafruit-ht16k33 fixture.
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
# data: the bytes come out of the same adafruit_ht16k33 sources the compiler is
# given.
#
# The HT16K33 is a write-only LED driver: after the I2CDevice probe (a
# zero-length write) every transaction carries data -- the oscillator-on
# command 0x21, the blink/display-on command 0x81, the brightness command
# 0xE0|xbright, and the 17-byte display-buffer dumps from show(). No read
# script is needed; readfrom_into below exists only so an unexpected read is
# still recorded instead of crashing the reference run.
#
# The program's `for _ in range(2)` returns on its own after exactly TXN_CAP
# transactions; the fake bus still raises _Done at the cap so a stream that
# grew unexpectedly cannot hang the reference run. The program's own print()
# goes to stderr so the oracle's stdout stays a clean transaction list.
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

# Construction is 5 transactions once (the I2CDevice probe, init's fill-show,
# and the oscillator/blink/brightness command writes); each loop iteration is
# then 13 -- the explicit fill's show plus one show() per print/setitem/
# property/set_digit_raw call. 31 is init plus two full iterations, so the
# repeating writes are part of the comparison.
TXN_CAP = 31


class _Done(BaseException):
    """Raised out of the bus once enough transactions have been recorded.

    BaseException, not Exception: the program under test wraps the constructor
    and the writes in `except Exception`, which would swallow a plain Exception
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
        # The HT16K33 never reads; an unexpected read still gets a well-defined
        # answer so the transaction is recorded rather than crashing the run.
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

    # The library's non_blocking_marquee reads time.monotonic() -- a path this
    # program never reaches -- and i2c_device's probe-retry sleeps; under the
    # oracle both are no-ops. time is a real module, so patch the attributes
    # instead of the module table.
    import time as _time
    _time.sleep = lambda *_a, **_kw: None
    _time.monotonic = lambda: 0.0

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
    # The program prints on the retry path; keep stdout a pure transaction
    # list by sending the program's prints to stderr.
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
