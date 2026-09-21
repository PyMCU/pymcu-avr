# PyMCU -- struct-tuple-return: `return struct.unpack_from(fmt, buf)` carrying the
# multi-field result out of an inline expansion, the shape adafruit_register's
# i2c_struct.Struct writes as its descriptor protocol:
#
#     def __get__(self, obj, objtype=None):
#         return struct.unpack_from(self.format, memoryview(_BUFFER)[1:])
#
#     def __set__(self, obj, value):
#         struct.pack_into(self.format, _BUFFER, 1, *value)
#
# The fixture keeps the shape and drops the bus: _BUF is a plain bytearray, so a
# __set__ writes the bytes a __get__ then reads back -- a round trip that needs no
# device to be exact.
#
#   A,B  a, b = f() -- the result slots keep their field widths (u16, not u8)
#   C    f()[k] reads one slot
#   P    print(f()) writes the tuple text CPython prints
#   F    "...".format(*f()) splices the result slots into the placeholders
#   S    d.pair = (...) calls __set__; a, b = d.pair calls __get__ and unpacks
#   D    d.pair[0] indexes the descriptor's tuple
#   R    "...{}...{}...".format(*d.pair) -- the register_simpletest line
#   T    print(d.pair) writes the tuple text
#   V    a second d.pair = (...) re-runs __set__ -- the round trip is live
#
# Buffer seeded from GPIOR0 (seed 5): _BUF[1..4] = 5,6,7,8, so the plain-function
# half prints <HH little-endian reads of bytes that nothing folded.
#
# Expected UART output, seed 5:
#   A 1541      (5 | 6<<8)
#   B 2055      (7 | 8<<8)
#   C 1541
#   P (1541, 2055)
#   F 1541:2055
#   S 4660 32768   (__set__ packed 0x1234/0x8000; __get__ read them back)
#   D 4660
#   R register 1: 4660; register 2: 32768
#   T (4660, 32768)
#   V register 1: 255; register 2: 1
#   END
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8
import struct

_BUF = bytearray(8)


def read_pair():
    return struct.unpack_from("<HH", memoryview(_BUF)[1:])


class Reg:
    def __init__(self, fmt):
        self.format = fmt

    def __get__(self, obj, objtype=None):
        return struct.unpack_from(self.format, memoryview(_BUF)[1:])

    def __set__(self, obj, value):
        struct.pack_into(self.format, _BUF, 1, *value)


class Dev:
    pair = Reg("<HH")


def main():
    base: uint8 = GPIOR0.value
    _BUF[1] = base
    _BUF[2] = base + 1
    _BUF[3] = base + 2
    _BUF[4] = base + 3

    a, b = read_pair()
    print("A", a)
    print("B", b)
    print("C", read_pair()[0])
    print("P", read_pair())
    print("F {}:{}".format(*read_pair()))

    d = Dev()
    d.pair = (0x1234, 0x8000)
    x, y = d.pair
    print("S", x, y)
    print("D", d.pair[0])
    print("R register 1: {}; register 2: {}".format(*d.pair))
    print("T", d.pair)

    d.pair = (0x00FF, 1)
    print("V register 1: {}; register 2: {}".format(*d.pair))

    print("END")


main()
