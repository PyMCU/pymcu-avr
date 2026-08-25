# PyMCU -- list-field: a class can declare a list field (PyMCU#106)
#
# `self.buf: list[uint8] = [0, 0, 0]` used to report "Array size 'uint8' is not a
# compile-time constant" -- a message about a size the program never wrote, since the
# bracket in list[T] holds the ELEMENT type. The bare `self.buf = [0, 0, 0]` reported
# "Unknown Expression type: ListExpr", an AST class name. The same list written as a
# function local compiled and ran, so the restriction was on the field position.
#
# Both spellings are now the fixed array the literal describes: as many elements as the
# literal has, of the annotated element type, or of the type the widest literal needs
# when there is no annotation.
#
# Every stored value comes from GPIOR0/GPIOR1, which the test writes before the run: a
# fixture of literals would measure the constant folder rather than the field.
#
# Five checkpoints, each leaving its answer in GPIOR2:
#   1 -- annotated uint8 field, element 0    2 -- annotated uint8 field, element 2
#   3 -- unannotated field, element 1        4 -- 300 + seed, high byte  (uint16 field)
#   5 -- 300 + seed, low byte  (a uint8 element type would have lost the high one)
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2
from pymcu.types import asm, uint8, uint16


class Annotated:
    # Two parameters rather than k and k + 1: arithmetic on a constructor parameter
    # stored into an instance-member array is rejected today ("'k' names a register"),
    # for the pre-existing uint8[N] spelling as much as for this one. Out of scope here.
    def __init__(self, k0: uint8, k2: uint8):
        self.buf: list[uint8] = [0, 0, 0]
        self.buf[0] = k0
        self.buf[2] = k2

    def first(self) -> uint8:
        return self.buf[0]

    def third(self) -> uint8:
        return self.buf[2]


class Bare:
    def __init__(self, k: uint8):
        self.buf = [0, 0, 0]
        self.buf[1] = k

    def second(self) -> uint8:
        return self.buf[1]


class Wide:
    def __init__(self, k: uint16):
        self.buf: list[uint16] = [0, 0]
        self.buf[1] = k

    def second_high(self) -> uint8:
        return self.buf[1] >> 8

    def second_low(self) -> uint8:
        return self.buf[1] & 0xFF


def main() -> None:
    seed0: uint8 = GPIOR0.value
    seed1: uint8 = GPIOR1.value

    third_seed: uint8 = seed1 + 1
    a = Annotated(seed0, third_seed)
    b = Bare(seed1)

    # Widened through the uint8 local: written as GPIOR0.value + 300 the load takes TWO
    # bytes from the register address, which is a separate bug and not what is under test.
    wide_seed: uint16 = 300 + seed0
    w = Wide(wide_seed)

    GPIOR2.value = a.first()
    asm("BREAK")

    GPIOR2.value = a.third()
    asm("BREAK")

    GPIOR2.value = b.second()
    asm("BREAK")

    GPIOR2.value = w.second_high()
    asm("BREAK")

    GPIOR2.value = w.second_low()
    asm("BREAK")

    while True:
        pass


main()
