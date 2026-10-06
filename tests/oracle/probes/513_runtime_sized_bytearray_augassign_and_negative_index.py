# expect: match
# doc: https://docs.pymcu.org/limitations/#dynamic-memory-and-containers
# A runtime-sized bytearray lives in the arena, and its element is the buffer's offset plus the
# index. `x[i] += v` had no arena hook, so it was refused as "Bit index must be constant for
# augmented assignment", and `x[-1]` added -1 to the offset and read the byte in front of the
# buffer. Both as a local and as an @inline __init__'s field.
from pymcu.types import uint8, uint16, inline
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value


class Dev:
    @inline
    def __init__(self, n: uint16):
        self.buf = bytearray(n)

    def bump(self, i: uint8):
        self.buf[i] += 10

    def tail(self) -> uint8:
        return self.buf[-1]


a = bytearray(s + 3)
d = Dev(s + 5)
a[-1] = s + 50
a[-1] += s + 5
a[s] |= 0x81
d.buf[-1] = s + 70
d.bump(s + 4)
d.buf[-2] = s + 9
d.buf[-2] -= 1
print(a[2], a[0], d.tail(), d.buf[3], len(d.buf))
print("END")
