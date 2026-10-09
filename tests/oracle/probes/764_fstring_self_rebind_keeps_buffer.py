# expect: match
# doc: https://docs.pymcu.org/roadmap/
# `s = f"..."` after `s = f"..."`: the rebind reuses the runtime string buffer
# rather than clearing its layout record. CPython prints new7.
from pymcu.chips.atmega328p import GPIOR0


def f():
    s = f"old{GPIOR0.value}"
    s = f"new{GPIOR0.value + 7}"
    print(s)


f()
print("END")
