# PyMCU -- stdlib-bare-imports: the standard spelling of a stdlib module.
#
# Regression for PyMCU#58. `import math` and `import random` reported
# "Module not found -- install it with `pymcu install math`", advice nobody could
# follow: they are not libraries, they are the stdlib under the name every Python
# program types (pymcu.math, pymcu.random).
#
# Both spellings are exercised, and the results are checked rather than the build:
#   GPIOR0 (0x3E) = map_range(512, 0, 1023, 0, 100) = 50
#   GPIOR1 (0x4A) = randint(3, 3)                   = 3   (a one-value range is exact)
#   GPIOR2 (0x4B) = randint(0, 10) seeded from GPIOR0, in [0, 10]
#
import math
from random import randint, seed
from pymcu.types import uint8, uint16, uint32, asm
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2


def main():
    scaled: uint16 = math.map_range(512, 0, 1023, 0, 100)
    GPIOR0.value = uint8(scaled)

    fixed: uint16 = randint(3, 3)
    GPIOR1.value = uint8(fixed)

    seed(uint32(12345))
    r: uint16 = randint(0, 10)
    GPIOR2.value = uint8(r)

    asm("BREAK")
    while True:
        pass
