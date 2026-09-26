# expect: match
# doc: https://github.com/PyMCU/PyMCU/issues/513
# The value half of 402.
from pymcu.chips.atmega328p import GPIOR0


def scan(n):
    last = None
    i = 0
    total = 0
    while i < n:
        pos = (i // 2) * 2 + GPIOR0.value
        changed = last is None or pos != last
        if changed:
            total = total + 1
            last = pos
        i = i + 1
    return total


print(scan(4))
print("END")
