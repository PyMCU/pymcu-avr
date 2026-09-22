#!/usr/bin/env python3
# CPython oracle for the adafruit-bmp280-unmodified fixture.
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
# data: the bytes come out of the same adafruit_bmp280.py / adafruit_bus_device
# sources the compiler is given.
#
# The fake bus is a BMP280 register file, not a dumb ACK: the driver validates
# the chip-id register (0xD0 == 0x58), reads the 24-byte calibration block at
# 0x88 and the 3-byte sensor bursts at 0xF7/0xFA, so reads must answer with a
# real register map. The AVR side replays the same map byte for byte (see
# TwiRegisterFile in tests/testkit); read data is part of the recorded stream,
# so the two maps must stay identical.
#
# Usage: .venv/bin/python oracle.py   (cwd does not matter; paths are __file__-relative)

import os
import runpy
import sys
import types
import typing

SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src")
SRC = os.path.normpath(SRC)

# (address, is_write, payload) in wire order.
TRANSACTIONS = []

# BMP280 register file shared with the C# TwiRegisterFile -- keep byte-identical.
#
# 0x88..0x9F calibration, struct.unpack("<HhhHhhhhhhhh"):
#   dig_T1=27504 dig_T2=26435 dig_T3=-1000
#   dig_P1=36477 dig_P2=-10685 dig_P3=3024 dig_P4=2855 dig_P5=140 dig_P6=-7
#   dig_P7=15500 dig_P8=-14600 dig_P9=6000
# (the datasheet example set; the compensation divisor var1 ends up non-zero,
# so pressure() returns a float rather than raising ArithmeticError).
_CALIBRATION = bytes([
    0x70, 0x6B, 0x43, 0x67, 0x18, 0xFC, 0x7D, 0x8E,
    0x43, 0xD6, 0xD0, 0x0B, 0x27, 0x0B, 0x8C, 0x00,
    0xF9, 0xFF, 0x8C, 0x3C, 0xF8, 0xC6, 0x70, 0x17,
])


def _bmp280_registers():
    regs = bytearray(256)
    regs[0x88:0x88 + len(_CALIBRATION)] = _CALIBRATION
    regs[0xD0] = 0x58            # _CHIP_ID: the driver refuses anything else
    regs[0xF3] = 0x00            # status: bit3 measuring=0, bit0 im_update=0
    regs[0xF7:0xFA] = bytes([0x65, 0x19, 0x00])   # press raw 0x65190
    regs[0xFA:0xFD] = bytes([0x7E, 0x40, 0x00])   # temp  raw 0x7E400
    return regs


class FakeI2C:
    """Stands in for CircuitPython busio.I2C with a BMP280 register file.

    Register-pointer semantics match the chip: the first byte of a write
    transaction latches the register address, further write bytes land at
    auto-incrementing addresses, and each read byte returns regs[ptr] then
    increments it. A zero-length write (the I2CDevice probe) records as an
    empty write transaction.
    """

    def __init__(self, scl=None, sda=None, *, frequency=400000, timeout=255):
        self._locked = False
        self._regs = _bmp280_registers()
        self._ptr = 0

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
        if data:
            self._ptr = data[0]
            for b in data[1:]:
                self._regs[self._ptr & 0xFF] = b
                self._ptr = (self._ptr + 1) & 0xFF

    def readfrom_into(self, address, buffer, *, start=0, end=None):
        if end is None:
            end = len(buffer)
        n = end - start
        data = bytes(self._regs[(self._ptr + i) & 0xFF] for i in range(n))
        self._ptr = (self._ptr + n) & 0xFF
        buffer[start:end] = data
        TRANSACTIONS.append((address, False, data))

    def writeto_then_readfrom(self, address, out_buffer, in_buffer, *,
                              out_start=0, out_end=None,
                              in_start=0, in_end=None):
        # On the wire: write phase, repeated START, read phase, one STOP. The
        # recorder closes a transaction at a repeated start, so this lands as a
        # write transaction followed by a read transaction.
        out = self._window(out_buffer, out_start, out_end)
        TRANSACTIONS.append((address, True, out))
        if out:
            self._ptr = out[0]
            for b in out[1:]:
                self._regs[self._ptr & 0xFF] = b
                self._ptr = (self._ptr + 1) & 0xFF
        if in_end is None:
            in_end = len(in_buffer)
        n = in_end - in_start
        data = bytes(self._regs[(self._ptr + i) & 0xFF] for i in range(n))
        self._ptr = (self._ptr + n) & 0xFF
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


def main():
    _install_fake_modules()
    sys.path.insert(0, SRC)
    # The program prints to stdout; keep that off the transaction stream so
    # the parser sees only "<addr> <data>" lines.
    import contextlib, io
    with contextlib.redirect_stdout(io.StringIO()):
        runpy.run_path(os.path.join(SRC, "main.py"), run_name="__main__")

    for address, is_write, data in TRANSACTIONS:
        if is_write:
            print(f"{address:02X} {data.hex()}")
        else:
            print(f"{address:02X} R {data.hex()}")


if __name__ == "__main__":
    main()
