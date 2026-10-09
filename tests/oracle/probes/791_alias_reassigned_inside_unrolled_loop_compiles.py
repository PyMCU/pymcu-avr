# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# `for i in range(2): if i == 1: alias = other` then `head(alias)`: range(2)
# with a cheap body unrolls at compile time, so `if i == 1:` folds to a
# compile-time-constant condition on each unrolled copy (always False on the
# first, always True on the second) -- there is no real branch join here, the
# reassignment is unconditional once unrolling resolves it, so this is NOT
# the ambiguous shape 789/790/792 refuse. CPython's answer is other's first
# byte (7), since the loop always reaches i == 1.
from pymcu.types import uint8


def head(v: bytearray) -> uint8:
    return v[0]


buf = bytearray([10, 20])
other = bytearray([7])
alias = buf
for i in range(2):
    if i == 1:
        alias = other
print(head(alias))
print("END")
