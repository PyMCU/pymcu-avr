# PyMCU -- builtin-bool: bool(x) is Python's truth test, which here is "not zero".
#
# The value under test is read back out of GPIOR0 so the compiler cannot fold the
# call away: what the checkpoints measure is the generated comparison, not a
# constant the front end computed.
#
# Data-space addresses (ATmega328P): GPIOR0 = 0x3E, GPIOR1 = 0x4A, GPIOR2 = 0x4B
#
# Checkpoints:
#   1. bool(0) is 0 and bool(non-zero) is 1
#   2. a value whose low byte is zero is still truthy at 16 bits (bool(256) is 1)
#   3. bool() composes with arithmetic: bool(a) + bool(b) counts the non-zero ones
#   4. a negative int is truthy
#
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2
from pymcu.types import uint8, uint16, int16, asm


def main():
    # --- Checkpoint 1: zero is false, anything else is true ---
    GPIOR0.value = 0
    z: uint8 = GPIOR0.value
    GPIOR1.value = bool(z)          # 0
    GPIOR0.value = 200
    nz: uint8 = GPIOR0.value
    GPIOR2.value = bool(nz)         # 1
    asm("BREAK")

    # --- Checkpoint 2: the test is on the whole value, not its low byte ---
    GPIOR0.value = 1
    hi: uint16 = GPIOR0.value
    hi = hi << 8                    # 256: low byte zero, value non-zero
    GPIOR1.value = bool(hi)         # 1
    asm("BREAK")

    # --- Checkpoint 3: the result is a 0/1 that arithmetic can use ---
    GPIOR0.value = 5
    a: uint8 = GPIOR0.value
    GPIOR0.value = 0
    b: uint8 = GPIOR0.value
    GPIOR1.value = bool(a) + bool(b)    # 1
    GPIOR2.value = bool(a) + bool(a)    # 2
    asm("BREAK")

    # --- Checkpoint 4: a negative value is truthy ---
    GPIOR0.value = 7
    n: int16 = GPIOR0.value
    n = 0 - n                       # -7
    GPIOR1.value = bool(n)          # 1
    asm("BREAK")

    while True:
        pass
