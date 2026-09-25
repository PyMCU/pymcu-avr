# PyMCU -- ptr-field-element-width: PyMCU#484 and PyMCU#485.
#
# A field holding a register pointer has an element width, and `.value` on it must be an
# access of exactly that width. Bound from a bare ptr(), from a ptr[T] parameter or from a
# register name the chip file exports, the field lost that width and kept the one the class
# scan had guessed -- UINT16, because what such a field holds is an address -- so every
# `.value` write put TWO bytes into I/O space and landed the second one on the neighbouring
# register. The only binding that survived was a `-> ptr[T]` selector function, because
# there the width travelled with the return annotation.
#
# GPIOR1 (0x4A) and GPIOR2 (0x4B) are adjacent general-purpose I/O registers with no side
# effects, so GPIOR2 is a witness: it is seeded with 0xA5 before each write through a field
# aimed at GPIOR1, and it must still read 0xA5 afterwards. A 16-bit store clears it.
#
# The fourth field pins the spelling that used to be refused outright with "Array size
# 'uint8' is not a compile-time constant": the annotated one, `self.reg: ptr[uint8] = ...`.
# The fifth pins that an annotation asking for a PAIR still gets a pair: TCNT1 is written
# through an annotated ptr[uint16] field and read back whole.
#
# Expected UART output:
#   17 165
#   18 165
#   19 165
#   20 165
#   4660
#   done
from pymcu.chips.atmega328p import GPIOR1, GPIOR2, TCNT1
from pymcu.hal.console import print
from pymcu.types import uint8, uint16, ptr, const, inline


class Hw:
    @inline
    def __init__(self, base: const[uint16], reg: ptr[uint8]):
        self.bare = ptr(base)            # bare ptr(): element width is one byte
        self.param = reg                 # ptr[uint8] parameter
        self.named = GPIOR1              # a register name from the chip file
        self.annotated: ptr[uint8] = GPIOR1
        self.cnt: ptr[uint16] = ptr(0x84)


h: Hw = Hw(0x4A, GPIOR1)

GPIOR2.value = 0xA5
h.bare.value = 17
a: uint8 = GPIOR1.value
b: uint8 = GPIOR2.value
print(a, b)

GPIOR2.value = 0xA5
h.param.value = 18
c: uint8 = GPIOR1.value
d: uint8 = GPIOR2.value
print(c, d)

GPIOR2.value = 0xA5
h.named.value = 19
e: uint8 = GPIOR1.value
f: uint8 = GPIOR2.value
print(e, f)

GPIOR2.value = 0xA5
h.annotated.value = 20
g: uint8 = GPIOR1.value
i: uint8 = GPIOR2.value
print(g, i)

h.cnt.value = 0x1234
j: uint16 = TCNT1.value
print(j)

print("done")
