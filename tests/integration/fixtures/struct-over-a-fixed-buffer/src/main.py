# PyMCU -- struct-over-a-fixed-buffer: the three shapes adafruit_register.i2c_struct writes.
#
# PyMCU#361. `struct` expands at compile time from a literal format, and three spellings were
# out of reach:
#
#   1. the result bound to a NAME and indexed through it. Refused as "returns a tuple, and
#      there is no heap to hold one" -- true of the tuple and not of the program, whose every
#      element is a scalar the format already sized and which is only ever read by constant
#      index. Struct.__get__ ends in `return struct.unpack_from(...)`, so the tuple travels as
#      a name before anyone indexes it.
#   2. `memoryview(buf)[1:]` as the buffer. Refused with "Slice indexing is only supported on
#      named fixed-size arrays", a sentence that never says memoryview and carries no caret. A
#      view of a fixed buffer at a constant offset IS the offset argument.
#   3. a multi-field format, read one field per index.
#
# The format arrives as a FIELD in every one of them, which is how a descriptor holds it.
#
# Every number is CPython's answer for the same lines, with the buffer 00 12 34 56 78.
#
#   big.get()    ">H" over 12 34   ->  4660
#   little.get() "<H" over 12 34   ->  13330
#   pair.hi()    ">HH" field 0     ->  4660
#   pair.lo()    ">HH" field 1     ->  22136
import struct

_BUFFER = bytearray([0x00, 0x12, 0x34, 0x56, 0x78])


class Struct:
    def __init__(self, fmt: str) -> None:
        self.format = fmt

    def get(self) -> int:
        v = struct.unpack_from(self.format, memoryview(_BUFFER)[1:])
        return v[0]


class Pair:
    def __init__(self, fmt: str) -> None:
        self.format = fmt

    def hi(self) -> int:
        v = struct.unpack_from(self.format, _BUFFER, 1)
        return v[0]

    def lo(self) -> int:
        v = struct.unpack_from(self.format, _BUFFER, 1)
        return v[1]


big = Struct(">H")
little = Struct("<H")
pair = Pair(">HH")


def main() -> None:
    print(big.get())
    print(little.get())
    print(pair.hi())
    print(pair.lo())
    print("done")
