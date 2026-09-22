# The same Conway's Game of Life as the compat-cp-life fixture, with the grid
# walked as the nested loops a newcomer would write -- `for y in range(8): for x
# in range(32):` instead of one flat `for k in range(256)`. The compiler's unroll
# policy must keep the outer loop a counter loop (its body holds another loop);
# before it, eight unrolled copies of the inner loop's inlined fill_rect blew past
# the Uno's SRAM. The oracle stream is byte-identical to the flat fixture's: same
# rules, same seeds, same GENERATIONS+1 framebuffer transactions.
import time

import board

import adafruit_ssd1306

CELL = 4
GENERATIONS = 8

i2c = board.I2C()
display = adafruit_ssd1306.SSD1306_I2C(128, 32, i2c)

cells = bytearray(256)
nxt = bytearray(256)


def seed():
    for k in range(256):
        cells[k] = 0
    # R-pentomino near the middle: .XX / XX. / .X.   (row * 32 + column)
    cells[3 * 32 + 15] = 1
    cells[3 * 32 + 16] = 1
    cells[4 * 32 + 14] = 1
    cells[4 * 32 + 15] = 1
    cells[5 * 32 + 15] = 1
    # a glider heading down-right from the top-left corner
    cells[0 * 32 + 1] = 1
    cells[1 * 32 + 2] = 1
    cells[2 * 32 + 0] = 1
    cells[2 * 32 + 1] = 1
    cells[2 * 32 + 2] = 1


def draw():
    display.fill(0)
    for y in range(8):
        for x in range(32):
            if cells[y * 32 + x]:
                display.fill_rect(x * CELL, y * CELL, CELL, CELL, 1)
    display.show()


def step() -> int:
    changed = 0
    alive = 0
    for y in range(8):
        for x in range(32):
            left = (x + 31) & 31
            right = (x + 1) & 31
            up = ((y + 7) & 7) << 5
            down = ((y + 1) & 7) << 5
            row = y << 5
            n = (cells[up + left] + cells[up + x] + cells[up + right]
                 + cells[row + left] + cells[row + right]
                 + cells[down + left] + cells[down + x] + cells[down + right])
            here = cells[row + x]
            if here:
                new = 1 if (n == 2 or n == 3) else 0
            else:
                new = 1 if n == 3 else 0
            nxt[row + x] = new
            if new != here:
                changed = changed + 1
            alive = alive + new
    for k in range(256):
        cells[k] = nxt[k]
    if alive == 0 or changed == 0:
        return 0
    return 1


seed()
draw()
for g in range(GENERATIONS):
    if step() == 0:
        time.sleep(1)
        seed()
    draw()
    time.sleep(0.1)
while True:
    pass
