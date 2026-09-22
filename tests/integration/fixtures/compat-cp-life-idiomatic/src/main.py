# Conway's Game of Life on the OLED through the unmodified adafruit_ssd1306 +
# adafruit_framebuf pair, written the way a CircuitPython user writes it: a Life
# class holding the two cell grids, a neighbours() count, nested
# `for y in range(self.height): for x in range(self.width)` walks, an LCG for the
# seed (the random module differs between interpreters; the LCG is identical on
# CPython, CircuitPython and PyMCU), and time.monotonic() pacing one generation
# every 0.1 s. 32x8 cells of 4x4 pixels on the 128x32 panel, toroidal edges.
# Runs GENERATIONS generations then parks in `while True: pass`, so every side
# emits exactly GENERATIONS+2 framebuffer transactions.
import time

import board

import adafruit_ssd1306

CELL = 4
GENERATIONS = 30
SEED = 20260921

i2c = board.I2C()
display = adafruit_ssd1306.SSD1306_I2C(128, 32, i2c)


class Life:
    """Conway's Game of Life on a toroidal width x height grid of cells."""

    def __init__(self, width, height, seed):
        self.width = width
        self.height = height
        self.cells = bytearray(width * height)
        self.next_cells = bytearray(width * height)
        self.rng = seed

    def seed(self):
        # A small LCG in Python, not the random module: CPython, CircuitPython
        # and PyMCU all compute the same 31-bit sequence, so the worlds match.
        for y in range(self.height):
            for x in range(self.width):
                self.rng = (self.rng * 1103515245 + 12345) & 0x7FFFFFFF
                self.cells[y * self.width + x] = (
                    1 if ((self.rng >> 16) & 3) == 0 else 0
                )

    def neighbours(self, x, y):
        n = 0
        for dy in range(-1, 2):
            for dx in range(-1, 2):
                if dx == 0 and dy == 0:
                    continue
                xx = (x + dx) % self.width
                yy = (y + dy) % self.height
                n = n + self.cells[yy * self.width + xx]
        return n

    def step(self):
        for y in range(self.height):
            for x in range(self.width):
                n = self.neighbours(x, y)
                if self.cells[y * self.width + x]:
                    self.next_cells[y * self.width + x] = (
                        1 if n == 2 or n == 3 else 0
                    )
                else:
                    self.next_cells[y * self.width + x] = 1 if n == 3 else 0
        for y in range(self.height):
            for x in range(self.width):
                self.cells[y * self.width + x] = self.next_cells[
                    y * self.width + x
                ]

    def draw(self, display):
        display.fill(0)
        for y in range(self.height):
            for x in range(self.width):
                if self.cells[y * self.width + x]:
                    for py in range(CELL):
                        for px in range(CELL):
                            display.pixel(
                                x * CELL + px, y * CELL + py, 1
                            )
        display.show()


life = Life(32, 8, SEED)
life.seed()
life.draw(display)

for g in range(GENERATIONS):
    tick = time.monotonic()
    life.step()
    life.draw(display)
    if g % 10 == 9:
        print("generation", g + 1)
    while time.monotonic() - tick < 0.1:
        pass

while True:
    pass
