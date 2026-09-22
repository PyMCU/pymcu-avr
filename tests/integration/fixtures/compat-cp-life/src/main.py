# Conway's Game of Life on the OLED through the unmodified adafruit_ssd1306 +
# adafruit_framebuf pair. A 32x8 grid of 4x4-pixel cells (256 cells, two bytearrays)
# on the 128x32 panel, toroidal edges, one generation per show(). Seeded with an
# R-pentomino and a glider; when the world dies or freezes it reseeds. Uses fill(),
# fill_rect() and show() only. The grid is walked with ONE flat loop over the 256
# cells (x = k & 31, y = k >> 5). The test loop is bounded at GENERATIONS and ends
# in `while True: pass`, so every side emits exactly GENERATIONS+1 framebuffer
# transactions: the CPython oracle, the AVR firmware and real CircuitPython all
# produce the same stream (oracle/ and the CompatCpLife tests compare byte for byte).
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
    for k in range(256):
        if cells[k]:
            x = k & 31
            y = k >> 5
            display.fill_rect(x * CELL, y * CELL, CELL, CELL, 1)
    display.show()


def step() -> int:
    changed = 0
    alive = 0
    for k in range(256):
        x = k & 31
        y = k >> 5
        left = (x + 31) & 31
        right = (x + 1) & 31
        up = ((y + 7) & 7) << 5
        down = ((y + 1) & 7) << 5
        row = y << 5
        n = (cells[up + left] + cells[up + x] + cells[up + right]
             + cells[row + left] + cells[row + right]
             + cells[down + left] + cells[down + x] + cells[down + right])
        here = cells[k]
        if here:
            new = 1 if (n == 2 or n == 3) else 0
        else:
            new = 1 if n == 3 else 0
        nxt[k] = new
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
