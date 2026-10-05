# expect: match
# doc: docs/language/roadmap.md
# An @inline callee's tuple results live in named result slots
# (<caller>.iret_<depth>_<seq>_<k>) that used to repeat for every expansion at the
# same depth. Two expansions consumed through sibling reads then shared the slot:
# `pair(3)[0] + pair(8)[0]` read the SECOND expansion's slot twice and answered
# 8+8=16 instead of 3+8=11. Same story for the constant-tracking fold: with
# literal arguments the stale constantVariables entry folded the read to the
# other call's value. The seed makes one half run-time so both paths are seen.
from pymcu.types import inline, uint8
from pymcu.chips.atmega328p import GPIOR0


@inline
def pair(v: uint8) -> (uint8, uint8):
    return v, v + 1


s = GPIOR0.value
print(pair(s + 3)[0] + pair(s + 8)[0])   # 3 + 8 = 11
print(pair(s + 4)[1] + pair(s + 7)[1])   # 5 + 8 = 13
print(pair(3)[0] + pair(8)[0])           # folded path: still 11
t = pair(s + 2)
u = pair(s + 6)
print(t[0] + u[0])                       # named copies: 2 + 6 = 8
print("END")
