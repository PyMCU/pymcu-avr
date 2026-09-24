# Loop twin of adafruit-ssd1306-unmodified-64: the same 128x64 geometry
# program -- fill, three pixel() calls, text(), two show()s -- inside
# `while True:`, the shape flashed firmware actually has. The wire trace is
# bounded: after LOOP_ITERS iterations the program spins in `while True: pass`,
# so CPython, the emulated Uno and real CircuitPython all emit the same
# transaction stream.
# SPDX-License-Identifier: CC0-1.0
import board

import adafruit_ssd1306

LOOP_ITERS = 3

i2c = board.I2C()
display_width = 128
display_height = 64
display = adafruit_ssd1306.SSD1306_I2C(display_width, display_height, i2c)

iters = 0
while True:
    display.fill(0)
    display.show()
    display.pixel(0, 0, 1)
    display.pixel(64, 16, 1)
    display.pixel(127, 31, 1)
    display.text("PyMCU", 0, 0, 1)
    display.show()
    iters = iters + 1
    if iters == LOOP_ITERS:
        while True:
            pass
