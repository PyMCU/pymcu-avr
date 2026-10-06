# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# Buffers had sixteen probes as a value, seven as a field and one as an argument, and none in
# a condition, as a subscript index or as a return. A driver reads a buffer in all three.
#
# The byte is seeded from GPIOR0 so the reads are not folded, and `tbl[buf[0]]` reads a cell
# whose value differs from the index, so an index that arrives wrong prints a wrong number
# rather than the index itself.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0

tbl = [10, 11, 12, 13]
buf = bytearray(4)
buf[0] = 2 + GPIOR0.value


def give() -> bytearray:
    return buf


def take(b: bytearray) -> uint8:
    return b[0]


class D:
    def __init__(self):
        self.buf = bytearray(4)
        self.buf[0] = 2 + GPIOR0.value


print(buf[0])
if buf[0] > 1:
    print(1)
else:
    print(0)
print(tbl[buf[0]])
if len(buf) > 3:
    print(1)
else:
    print(0)
print(give()[0])
print(take(buf))
print(tbl[take(buf)])
d = D()
print(tbl[d.buf[0]])
print("END")
