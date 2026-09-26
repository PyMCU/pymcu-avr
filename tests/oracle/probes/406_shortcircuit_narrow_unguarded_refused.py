# expect: refuse may be None here
# doc: https://github.com/PyMCU/PyMCU/issues/513
# The control for 400-405, and the reason those six assert anything at all. It is the
# same program with `last is None or` removed, so nothing proves `last` is live where
# `pos != last` reads it, and the compiler must still refuse. If this one ever starts
# compiling, the other six are passing through a path that no longer needs the
# narrowing, and their green would stop being evidence about #513.
from pymcu.chips.atmega328p import GPIOR0


def scan(n):
    last = None
    i = 0
    total = 0
    while i < n:
        pos = (i // 2) * 2 + GPIOR0.value
        if pos != last:
            total = total + 1
            last = pos
        i = i + 1
    return total


print(scan(4))
print("END")
