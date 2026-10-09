# expect: refuse names more than one buffer
# doc: https://docs.pymcu.org/limitations/#arithmetic
# The other truth value of 789: flag reads False here (still a volatile,
# non-foldable read), so the `if` body never runs and CPython's answer is
# buf's first byte (10) instead of other's -- but the refusal is the same
# either way, and has to be: the compiler decides this at compile time,
# before it knows which way the runtime flag will go.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


def head(v: bytearray) -> uint8:
    return v[0]


buf = bytearray([10, 20])
other = bytearray([7])
alias = buf
flag = GPIOR0.value != 0
if flag:
    alias = other
print(head(alias))
print("END")
