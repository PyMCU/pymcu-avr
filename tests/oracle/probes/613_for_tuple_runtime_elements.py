# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# `for xx in (x - 1, x, x + 1):` with a non-constant x: CPython builds the
# tuple by evaluating each element once, then iterates it. Before, any
# element that was not a compile-time constant refused the whole loop.
from pymcu.chips.atmega328p import GPIOR0

x = GPIOR0.value
x = x + 5
for xx in (x - 1, x, x + 1):
    print(xx)

# A tuple that mixes a constant with a run-time element: each one decides on
# its own terms whether it folds or runs.
for yy in (1, x, 9):
    print(yy)

print("END")
