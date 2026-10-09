# expect: refuse names more than one buffer
# doc: https://docs.pymcu.org/limitations/#arithmetic
# `if flag: alias = buf` with NO else, then `head(alias)` outside the if --
# flag reads True here (volatile, non-foldable), so CPython's answer is
# buf's first byte (10). Only ONE arm of the implicit if/else binds "alias"
# at all; the branch join still drops it (the other, empty arm never has
# the key), the same way disagreeing values do in 789/790. Before this
# probe's own fix the call silently read alias's own unbacked scalar slot --
# 0, regardless of the runtime flag -- so this is refused for the identical
# reason, even though a human reading the source (and knowing flag is
# always True here) can see the "ambiguity" never actually happens at
# runtime: the compiler cannot see that, only that one path leaves the name
# unbound.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


def head(v: bytearray) -> uint8:
    return v[0]


buf = bytearray([10, 20])
flag = GPIOR0.value == 0
if flag:
    alias = buf
print(head(alias))
print("END")
