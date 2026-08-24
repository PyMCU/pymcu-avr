# PyMCU -- class-param-function: free functions that take a class instance.
#
# Regression for PyMCU#71 and PyMCU#72, the two halves of one hole:
#   #71 a class in the FIRST parameter was never emitted, and the program failed
#       at link with `undefined reference` -- nothing said so before the linker.
#   #72 a class in ANY OTHER parameter position was lowered as an ordinary
#       function whose field reads were never bound, so it silently computed on
#       whatever the RAM held (the field read as zero).
#
# The field is seeded from a volatile I/O read so nothing folds at compile time:
# with GPIOR0 = 7 written before the run,
#   GPIOR1 (0x4A) = leer(c)                  = 7    (class as the only parameter)
#   GPIOR2 (0x4B) = leer_k(2, c) after poner = 12   (class after a numeric one,
#                                                    and the field mutated at 10
#                                                    through a free function)
#
from pymcu.types import uint8, asm
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2


class C:
    def __init__(self, n: uint8):
        self.n: uint8 = n


def leer(o: C) -> uint8:
    return o.n


def leer_k(k: uint8, o: C) -> uint8:
    return o.n + k


def poner(o: C, v: uint8):
    o.n = v


def main():
    seed: uint8 = GPIOR0.value
    c = C(seed)
    GPIOR1.value = leer(c)
    poner(c, leer(c) + 3)
    GPIOR2.value = leer_k(2, c)
    asm("BREAK")
    while True:
        pass
