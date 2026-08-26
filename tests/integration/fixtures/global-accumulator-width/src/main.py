# PyMCU -- global-accumulator-width: a module-level accumulator fed a wider value.
#
# Regression for PyMCU#205. `total = 0` at module level was typed uint8 from its initializer,
# and `total = total + r` with r uint16 inside a function wrote sixteen bits into eight. No
# error, no warning: a moving average wrapped and reported small plausible numbers.
#
# The width is decided before any function is lowered, and the pass that decides it only ever
# read module-level statements. Widening at the store cannot fix it -- by then the read of
# `total` on the right has already been lowered narrow.
#
# Two shapes, because the issue's own bounding table got the second one wrong: it recorded a
# literal reassignment as already correct, which is true only when the reassignment is also at
# module scope. From inside a function it truncated exactly like the accumulator.
#
# Seeded from GPIOR0 so the sum is not a compile-time constant. The LOW byte cannot discriminate
# -- 307 and 307 & 0xFF share it -- so the high byte is what each test reads.
#
# With GPIOR0 = 7:
#   total   = 300 + 7 = 307 = 0x0133   -> GPIOR1 = 0x01 (high), truncated would be 0x00
#   counter = 400     = 0x0190         -> GPIOR2 = 0x01 (high), truncated would be 0x00
from pymcu.types import uint16, asm
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2

total = 0
counter = 5


def accumulate() -> None:
    global total
    r: uint16 = 300 + GPIOR0.value
    total = total + r


def bump() -> None:
    global counter
    counter = 400


def main() -> None:
    accumulate()
    bump()
    GPIOR1.value = total >> 8
    GPIOR2.value = counter >> 8
    asm("BREAK")
    while True:
        pass
