# expect: match
# doc: docs/language/limitations.md
# `if n > 1.0:` on a NaN n used to take the THEN branch instead of CPython's else: an `if`'s
# jump-to-else used to negate a comparison by swapping to the algebraically opposite
# operator (NOT(a<b) == a>=b), true for every ORDERED pair but false whenever an operand is
# NaN (both a<b and a>=b are false for NaN, so the swap silently answered a different
# question). A float comparison now takes the DIRECT sense to a skip label instead of the
# swapped operator -- see EmitOrderedComparisonJump, ControlFlow.cs.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
n: float = float('nan') + float(s)

if n > 1.0:
    print("gt-then")
else:
    print("gt-else")

if n >= 1.0:
    print("ge-then")
else:
    print("ge-else")

if n < 1.0:
    print("lt-then")
else:
    print("lt-else")

if n <= 1.0:
    print("le-then")
else:
    print("le-else")

loops: uint8 = 0
while n > 1.0:
    loops = loops + 1
    if loops > 3:
        break
print(loops)
print("END")
