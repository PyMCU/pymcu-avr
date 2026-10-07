# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# `row = cells[y]` on an annotated list[list[uint8]] binds row to the SAME inner
# list CPython would: writing through row must be visible through cells[y],
# and the binding must survive inside a function frame and a loop that rebinds
# the name each iteration.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
cells: list[list[uint8]] = [[s, s + 1], [s + 2, s + 3]]
y: uint8 = 1
row = cells[y]
row[0] = 9
print(row[0])
print(cells[1][0])
print(cells[0][0])


def bump_first_row(grid: list[list[uint8]], v: uint8):
    r = grid[0]
    r[1] = v
    return r[0]


print(bump_first_row(cells, 42))
print(cells[0][1])

total = 0
for yy in range(2):
    r2 = cells[yy]
    r2[1] = yy + 7
    total = total + r2[1]
print(total)
print("END")
