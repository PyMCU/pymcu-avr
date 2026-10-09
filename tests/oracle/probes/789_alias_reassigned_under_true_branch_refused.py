# expect: refuse names more than one buffer
# doc: https://docs.pymcu.org/limitations/#arithmetic
# `alias = buf` then `if flag: alias = other` then `head(alias)`, with flag a
# volatile (non-foldable) read that happens to be True here. CPython picks
# other's first byte (7); PyMCU cannot: the branch join drops the alias
# because the two arms disagree on which buffer it names (BindSequenceAlias /
# BranchState.JoinDicts intersect on value agreement), and before this probe's
# own fix the call silently fell through to alias's own unbacked scalar slot
# -- 0, always, regardless of the runtime flag. Refused instead, the same
# unresolved-choice shape the ternary-of-two-buffers (787) and
# list-of-one-buffer (788) refusals already cover.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


def head(v: bytearray) -> uint8:
    return v[0]


buf = bytearray([10, 20])
other = bytearray([7])
alias = buf
flag = GPIOR0.value == 0
if flag:
    alias = other
print(head(alias))
print("END")
