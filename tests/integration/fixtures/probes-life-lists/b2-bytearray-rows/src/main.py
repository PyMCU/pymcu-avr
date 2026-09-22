# Probe (b2): same program as (a) but the rows are bytearrays held in a plain
# list, `self.cells = [bytearray(width) for _ in range(height)]` -- the
# bytearray-row grid spelling. It lowers to one flat uint8[256] and runs the
# same Life.
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

    def __init__(self, width, height):
        self.width = width
        self.height = height
        self.cells = [bytearray(width) for _ in range(height)]
        self.next_cells = [bytearray(width) for _ in range(height)]

    def seed(self):
        rng = SEED
        for y in range(self.height):
            for x in range(self.width):
                rng = (rng * 1103515245 + 12345) & 0x7FFFFFFF
                self.cells[y][x] = 1 if ((rng >> 16) & 3) == 0 else 0

    def neighbours(self, x, y):
        n = 0
        for dy in range(-1, 2):
            for dx in range(-1, 2):
                if dx == 0 and dy == 0:
                    continue
                xx = (x + dx) % self.width
                yy = (y + dy) % self.height
                n = n + self.cells[yy][xx]
        return n

    def step(self):
        for y in range(self.height):
            for x in range(self.width):
                n = self.neighbours(x, y)
                if self.cells[y][x]:
                    self.next_cells[y][x] = 1 if n == 2 or n == 3 else 0
                else:
                    self.next_cells[y][x] = 1 if n == 3 else 0
        for y in range(self.height):
            for x in range(self.width):
                self.cells[y][x] = self.next_cells[y][x]

    def draw(self, display):
        display.fill(0)
        for y in range(self.height):
            for x in range(self.width):
                if self.cells[y][x]:
                    for py in range(CELL):
                        for px in range(CELL):
                            display.pixel(
                                x * CELL + px, y * CELL + py, 1
                            )
        display.show()


life = Life(32, 8)
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
