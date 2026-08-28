# PyMCU -- float-local-overload: math.floor of a float LOCAL reads the local.
#
# Regression for PyMCU#214, which is upstream of PyMCU#182.
#
# InferExprType looked a name up under ONE qualification, `currentInlinePrefix + name`. A plain
# function local is registered as `main.x`, and at a top-level call site the inline prefix is
# empty, so the lookup missed and the function fell through to UINT8. Every float local has
# therefore always spelled itself "uint8" for overload selection: `math.floor(x)` could not
# match `floor(x: float)`, took the integer overload, and an integer floor over a value it read
# as zero returned zero. Three different inputs produced the same answer, which is the signature
# of an operand never read, and it was live and silent with a plausible small number coming out.
#
# The old arity fallback ignored the suffix and picked by enumeration order, which for `floor`
# happened to land on the float overload, so the wrong suffix was invisible until #182 made the
# fallback respect the numeric family. #182 did not cause this; it exposed it.
#
# +100 on every answer so a zero result is distinguishable from a correct small one: the defect
# printed 100 for all four seeds.
#
# GPIOR0 carries the seed and the TEST writes it. A literal would fold and measure the constant
# folder instead of the call. qemu does not retain a write to GPIOR0, so this is avr8sharp only.
#
#   seed   x = seed - 1.5   floor(x)   reported   unfixed
#   0      -1.5             -2          98         100
#   1      -0.5             -1          99         100
#   2       0.5              0         100         100
#   3       1.5              1         101         100
#
# Seed 2 is the row that would have hidden the defect on its own: there the wrong answer and the
# right answer are both 100. Four seeds, and the pair 0 and 3 is what makes it visible.
from pymcu.chips.atmega328p import GPIOR0, GPIOR1
from pymcu.types import asm, int32, uint8
import math


def main():
    seed: uint8 = GPIOR0.value
    x: float = float(seed) - 1.5

    f: int32 = math.floor(x)
    GPIOR1.value = uint8(f + 100)

    asm("BREAK")
    while True:
        pass


main()
