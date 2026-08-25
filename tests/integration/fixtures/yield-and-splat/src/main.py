# PyMCU -- yield-and-splat: two forms PyMCU#67 reported as parser positions.
#
# `yield` with no value died as "SyntaxError: Expected expression" although the generator
# lowering already handles a valueless suspension (it publishes 0). `f(*xs)` died the same
# way, although the elements can be spliced at compile time.
#
#   GPIOR0 (0x3E) = how many times a bare-yield generator suspended = 3
#   GPIOR1 (0x4A) = suma(*valores) with a named tuple  = 1+2+3 = 6
#   GPIOR2 (0x4B) = suma(*[4, 5, 6]) with a list       = 15
from pymcu.types import uint8, asm
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2

valores = (1, 2, 3)


def tres_pausas():
    i: uint8 = 0
    while i < 3:
        yield
        i = i + 1


def suma(a: uint8, b: uint8, c: uint8) -> uint8:
    return a + b + c


def main():
    veces: uint8 = 0
    for _ in tres_pausas():
        veces = veces + 1
    GPIOR0.value = veces

    GPIOR1.value = suma(*valores)
    GPIOR2.value = suma(*[4, 5, 6])

    asm("BREAK")
    while True:
        pass
