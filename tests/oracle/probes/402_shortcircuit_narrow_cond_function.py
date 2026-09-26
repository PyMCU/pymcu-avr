# expect: match
# doc: https://github.com/PyMCU/PyMCU/issues/513
# 400 inside a function. The narrowing record is per binding, so a name that lives in a
# frame is a different storage question from a module global, and #513's demandant
# (pymcu-circuitpython's 14_rotary_encoder) was this shape.
from pymcu.chips.atmega328p import GPIOR0


def scan(n):
    last = None
    i = 0
    total = 0
    while i < n:
        pos = (i // 2) * 2 + GPIOR0.value
        if last is None or pos != last:
            total = total + 1
            last = pos
        i = i + 1
    return total


print(scan(4))
print("END")
