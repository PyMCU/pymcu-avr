# genexp-all-tuple: all(0 <= component <= 255 for component in val)
#
# adafruit_pixelbuf.py:299 validates a colour with exactly this line before
# packing it. `val` arrives as a tuple argument, so the generator expression
# unrolls over the tuple's three elements at compile time -- and it must
# SHORT-CIRCUIT: (1, 2, 3) walks all three, (1, 2, 300) stops at the 300.
#
# WHAT DISCRIMINATES: prints 1, 0, 1, 0, END. A generator evaluated eagerly or
# one that misreads the chained comparison prints the wrong row.
from pymcu.types import uint8, inline
from pymcu.time import delay_ms


@inline
def check(val) -> uint8:
    if all(0 <= component <= 255 for component in val):
        return 1
    return 0


def main():
    while True:
        print(check((1, 2, 3)))
        print(check((1, 2, 300)))
        print(check((0, 255, 7)))
        print(check((256, 0, 0)))
        print("END")
        delay_ms(1200)
