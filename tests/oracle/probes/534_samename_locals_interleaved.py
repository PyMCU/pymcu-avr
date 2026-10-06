# expect: match
# doc: https://docs.pymcu.org/roadmap/
# Two functions may declare a local with the same source name: the IR qualifies
# each binding by its function (f.acc / g.acc) and by inline prefix
# (inline{d}.<caller>_<fn>.acc), so same-named locals never share identity or
# storage. Interleave an @inline expansion and a regular call that both own an
# `acc`, and run it under the poisoned cold boot the oracle applies: every call
# must answer its own value.
from pymcu.types import inline, uint8
from pymcu.chips.atmega328p import GPIOR0


@inline
def bump(v: uint8) -> uint8:
    acc = v + 1
    return acc


def twice(v: uint8) -> uint8:
    acc = v + 2
    return acc


s = GPIOR0.value
a = bump(s + 3)
b = twice(s + 5)
c = bump(s + 7)
d = twice(s + 9)
print(a)   # 4
print(b)   # 7
print(c)   # 8
print(d)   # 11
# the same interleave inside one expression: bump(s+1)=2, twice(s+2)=4
print(bump(s + 1) + twice(s + 2))         # 2 + 4 = 6
print("END")
