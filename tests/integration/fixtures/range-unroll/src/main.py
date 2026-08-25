# PyMCU -- range-unroll: for over a constant range unrolls (PyMCU#79)
#
# The parser files a plain `for i in range(a, b)` as RangeStart/RangeStop/RangeStep with
# no Iterable, and the unrolling in Iteration.cs only ever saw range() as an ITERABLE
# expression -- the shape enumerate() and zip() build. So the plain form always emitted a
# real loop and its variable never qualified where a compile-time constant is required:
# `Pin(p, Pin.OUT)` rejected `for p in range(11, 14)` while `pins = [11, 12, 13]` then
# `for p in pins:` compiled to the same three pins.
#
# Every loop below passes its variable to bit_mask(), whose parameter is const[uint8].
# That call does not compile at all unless the loop unrolled, so the fixture cannot be
# built by a compiler without the fix -- and the bits it produces come from GPIOR0, which
# the test seeds, so a run still has to reach each iteration with the right constant.
#
# Three checkpoints, each leaving its answer in GPIOR2:
#   1 -- range(3):            seed bits 0-2, in place
#   2 -- range(7, 4, -1):     a descending range, seed bits 0-2 landing in bits 5-7
#   3 -- range(WIDTH):        a bound that is a named constant rather than a literal
from pymcu.chips.atmega328p import GPIOR0, GPIOR2
from pymcu.types import asm, uint8, const, inline

WIDTH = 4


@inline
def bit_mask(b: const[uint8]) -> uint8:
    return 1 << b


def main() -> None:
    seed: uint8 = GPIOR0.value

    up: uint8 = 0
    for i in range(3):
        if (seed >> i) & 1:
            up = up | bit_mask(i)
    GPIOR2.value = up
    asm("BREAK")

    down: uint8 = 0
    for j in range(7, 4, -1):
        if (seed >> (j - 5)) & 1:
            down = down | bit_mask(j)
    GPIOR2.value = down
    asm("BREAK")

    named: uint8 = 0
    for k in range(WIDTH):
        if (seed >> k) & 1:
            named = named | bit_mask(k)
    GPIOR2.value = named
    asm("BREAK")

    while True:
        pass


main()
