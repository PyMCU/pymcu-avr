# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# The exact shape of Life.step()'s inner loop (PyMCU golperf): xx = (x + dx) % w
# wraps a toroidal row, w a class field folded to the literal 32 it was
# constructed with. x walks both edges (0 and w - 1) where the wraparound
# actually bites, and dx is -1, 0, 1, unrolled the same way `for dx in
# range(-1, 2)` unrolls in the real program. Printing xx (not just using it)
# pins the narrowed result's VALUE, and the surrounding sum mirrors
# `n = n + prev_row[xx] + old_row[xx] + next_value` so a wrong width on xx
# would also show up as a wrong index into a real array, not just a number.
from pymcu.types import int16, uint8


class Grid:
    def __init__(self, width):
        self.width = width


grid = Grid(32)
row = bytearray(32)
for i in range(32):
    row[i] = i

w: uint8 = grid.width
for x in range(w):
    total: int16 = 0
    for dx in range(-1, 2):
        xx: uint8 = (x + dx) % w
        total = total + row[xx]
    print(x, total)
print("END")
