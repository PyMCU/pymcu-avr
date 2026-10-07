# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# Probe 617 made the loop variable keep the tuple's LAST element after its own
# loop ends, on purpose, matching CPython. That leftover must not leak into a
# LATER, unrelated for-loop that rebinds the same bare name over a genuinely
# run-time sequence: `for x in (1, 173): pass` followed by
# `for i, x in enumerate(data):` over a fixed array read back 173 every
# iteration instead of data's own elements.
from pymcu.types import uint8

data: uint8[3] = [10, 20, 30]

for x in (1, 173):
    pass

total = 0
for i, x in enumerate(data):
    total = total + x
print(total)
print(x)
print("END")
