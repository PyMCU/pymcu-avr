# PyMCU -- struct-unpack: `t = struct.unpack(fmt, buf)` and
# `coeff = list(struct.unpack(fmt, bytes(buf)))` bind the multi-field result as a
# compile-time sequence of typed reads (#361) -- the coefficient-read shape
# adafruit_bmp280's `_read_coefficients` writes.
#
# Each field keeps its own width and sign: `h` fields read negative, `H` fields
# read unsigned, and a `[float(i) for i in coeff]` comprehension re-reads them.
# Slices of the rebound name land on `self` fields, which then index and report
# len() like any fixed sequence.
#
# The bytes are filled from GPIOR0 (seed 5) so nothing folds at compile time;
# raw[5] is then set to 0x80 so the `h` field at offset 4 goes negative.
#
# With seed 5, raw = [5,6,7,8,9,0x80,11,12,...,28].
#
# Expected UART output, seed 5:
#   A 1541   (H le @0: 5 | 6<<8)
#   B 2055   (h le @2: 7 | 8<<8)
#   C 2      (len)
#   D 1800   (H be @2: 7<<8 | 8)
#   E 2432   (h be @4: 9<<8 | 0x80)
#   N -32759 (h le @4: 9 | 0x80<<8 -- the signed field)
#   F 1541   (float coeff[0])
#   G -32759 (float coeff[2] -- sign survives the comprehension)
#   H 3083   (float coeff[3] == _pres[0])
#   I 3      (len _temp)
#   J 9      (len _pres)
#   K 7195   (_pres[8]: 27 | 28<<8)
#   M 1541   (load_calibration_coeffs -- long name hits symbol shortening)
#   L 11     (for-in sum of <BB: 5 + 6)
#   O 1800   (>H over memoryview(raw)[2:]: 7<<8 | 8 -- i2c_struct's shape)
#   P -32759 (<h over a named memoryview slice @4: 9 | 0x80<<8)
#   Q 1800   (>H over memoryview(raw) with offset arg 2)
#   END
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8, int16
import struct


class Cal:
    def __init__(self):
        self._temp = None
        self._pres = None

    def load(self, raw: bytearray):
        coeff = list(struct.unpack("<HhhHhhhhhhhh", bytes(raw)))
        coeff = [float(i) for i in coeff]
        self._temp = coeff[:3]
        self._pres = coeff[3:]

    # Long qualified name: the unrolled `coeff__N` elements exceed the 24-char
    # symbol-shortening threshold, so the tail after the last __ is a bare digit.
    # Symbol shortening must not emit `.equ 0` -- an invalid assembler name.
    def load_calibration_coeffs(self, raw: bytearray):
        coeff = list(struct.unpack("<Hhh", bytes(raw)))
        coeff = [float(i) for i in coeff]
        self._temp = coeff[:]


def main():
    base: uint8 = GPIOR0.value
    raw = bytearray(24)
    i: uint8 = 0
    while i < 24:
        raw[i] = base + i
        i = i + 1
    raw[5] = 0x80

    # Bare unpack bound to a name: fields keep their own width and sign.
    t = struct.unpack("<Hh", bytes(raw))
    print("A", t[0])
    print("B", t[1])
    print("C", len(t))

    # unpack_from with an offset, big-endian order.
    u = struct.unpack_from(">Hh", bytes(raw), 2)
    print("D", u[0])
    print("E", u[1])

    # A signed field read through a typed name.
    n = struct.unpack_from("<h", bytes(raw), 4)
    print("N", n[0])

    # The bmp280 shape: list(unpack) -> float comprehension -> slices to fields.
    c = Cal()
    c.load(raw)
    print("F", int(c._temp[0]))
    print("G", int(c._temp[2]))
    print("H", int(c._pres[0]))
    print("I", len(c._temp))
    print("J", len(c._pres))
    print("K", int(c._pres[8]))

    # Long-name variant: exercises symbol shortening on `coeff__N` elements.
    c.load_calibration_coeffs(raw)
    print("M", int(c._temp[0]))

    # Iterate the bound sequence.
    s = struct.unpack("<BB", bytes(raw))
    total: int16 = 0
    for b in s:
        total = total + b
    print("L", total)

    # memoryview(raw)[off:] -- a compile-time view whose start adds to the read
    # offset (i2c_struct's `unpack_from(fmt, memoryview(self._buf)[1:])`).
    v = struct.unpack_from(">H", memoryview(raw)[2:])
    print("O", v[0])

    # The same view bound to a name, then unpacked.
    mv = memoryview(raw)[4:]
    w = struct.unpack_from("<h", mv)
    print("P", w[0])

    # A plain memoryview with the offset given on unpack_from.
    x = struct.unpack_from(">H", memoryview(raw), 2)
    print("Q", x[0])

    print("END")


main()
