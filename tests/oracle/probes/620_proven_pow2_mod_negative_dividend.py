# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# PyMCU/PyMCU golperf: `(x + dx) % w` in a Game of Life step() narrows to `& 31`
# when the compiler proves w == 32 (a field written once, from a literal). The
# mask must answer Python's FLOOR modulo even when the dividend is negative:
# (-1) % 32 is 31 in CPython, not -1 and not the two's-complement reading of
# -1 at some other width. w is read through a field so the value reaches
# this probe the same way self.width does in the real program, and x is
# GPIOR0.value (seeded 0 by both runners) so the dividend is not itself a
# compile-time constant the folder could answer without the rewrite.
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import int16, uint8


class Wrap:
    def __init__(self, width):
        self.width = width


wrap = Wrap(32)
x: int16 = GPIOR0.value
w: uint8 = wrap.width

print((x - 1) % w)
print((x - 2) % w)
print((0 - x - 1) % w)
print((x + 31) % w)
print((x + 32) % w)
print("END")
