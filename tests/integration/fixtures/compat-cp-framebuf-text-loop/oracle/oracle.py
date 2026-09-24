#!/usr/bin/env python3
# CPython oracle for the adafruit-ssd1306-unmodified loop fixture.
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
# data: the bytes come out of the same adafruit_ssd1306.py / adafruit_framebuf.py
# / adafruit_bus_device sources the compiler is given.
#
# main.py loops `while True:` -- unbounded, the way flashed firmware runs.
# FakeI2C.writeto therefore raises _Done the moment the last expected 513-byte
# framebuffer write is recorded: one show() is one write of 0x40 plus the
# 512-byte MONO_VLSB buffer, and the stream carries LOOP_FRAMES+1 of them (the
# driver's __init__ shows the empty buffer once before the loop's own). The
# sentinel mechanically enforces the count: a program that emits more never
# reaches its tail here, and one that emits fewer hangs and is killed by the
# runner's timeout.
#
# Usage: .venv/bin/python oracle.py   (cwd does not matter; paths are __file__-relative)

import os
import runpy
import sys
import types
import typing

SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src")
SRC = os.path.normpath(SRC)

LOOP_FRAMES = 4      # loop iterations the oracle waits through
FRAME_BYTES = 513    # one show(): control byte 0x40 + 512 framebuffer bytes
# framebuffer writes on the wire: __init__'s show() + one per loop iteration
EXPECTED_FRAMES = LOOP_FRAMES + 1

# (address, is_write, payload) in wire order.
TRANSACTIONS = []


class _Done(Exception):
    """Internal control flow: the reference stream is complete."""


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
        self._frames = 0

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
        return bytes(memoryview(buffer)[start:end])

    def writeto(self, address, buffer, *, start=0, end=None):
        data = self._window(buffer, start, end)
        TRANSACTIONS.append((address, True, data))
        if len(data) == FRAME_BYTES:
            self._frames += 1
            if self._frames == EXPECTED_FRAMES:
                raise _Done

    def readfrom_into(self, address, buffer, *, start=0, end=None):
        if end is None:
            end = len(buffer)
        n = end - start
        data = bytes([0xFF] * n)
        buffer[start:end] = data
        TRANSACTIONS.append((address, False, data))

    def writeto_then_readfrom(self, address, out_buffer, in_buffer, *,
                              out_start=0, out_end=None,
                              in_start=0, in_end=None):
        # On the wire: write phase, repeated START, read phase, one STOP. The
        # recorder closes a transaction at a repeated start, so this lands as a
        # write transaction followed by a read transaction.
        TRANSACTIONS.append((address, True, self._window(out_buffer, out_start, out_end)))
        if in_end is None:
            in_end = len(in_buffer)
        n = in_end - in_start
        data = bytes([0xFF] * n)
        in_buffer[in_start:in_end] = data
        TRANSACTIONS.append((address, False, data))

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

    # i2c_device.py binds ReadableBuffer/WriteableBuffer for its method
    # annotations; without the package the try/except leaves them undefined and
    # class creation raises NameError on CPython.
    cpt = types.ModuleType("circuitpython_typing")
    cpt.ReadableBuffer = typing.Union[bytes, bytearray, memoryview]
    cpt.WriteableBuffer = typing.Union[bytearray, memoryview]
    sys.modules["circuitpython_typing"] = cpt

    # adafruit_ssd1306.py prefers MicroPython's framebuf when it imports. There
    # is no MicroPython framebuf here, and sys.modules[name] = None makes
    # `import framebuf` raise ImportError so the adafruit_framebuf fallback is
    # what runs -- deterministically, not just because the venv lacks the module.
    sys.modules["framebuf"] = None


def main():
    _install_fake_modules()
    sys.path.insert(0, SRC)
    # BitmapFont does a real open("font5x8.bin", "rb") here: under CPython that
    # resolves through the cwd, so run the program from the directory that holds
    # the font -- the same file pymcuc embeds into the firmware's flash.
    os.chdir(SRC)
    try:
        runpy.run_path(os.path.join(SRC, "main.py"), run_name="__main__")
    except _Done:
        # The last framebuffer write is recorded; the program's
        # `while True:` tail is unreachable here by construction.
        pass

    for address, is_write, data in TRANSACTIONS:
        if is_write:
            print(f"{address:02X} {data.hex()}")
        else:
            print(f"{address:02X} R {data.hex()}")


if __name__ == "__main__":
    main()
