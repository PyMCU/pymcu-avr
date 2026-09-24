# Same unmodified adafruit_ssd1306 + adafruit_framebuf pair as
# compat-cp-framebuf-text, but the program does its work inside `while True:` --
# the shape flashed firmware actually has, and the one the module-level fixture
# missed: a receiver method call in a loop walks the fields the method writes.
# The wire trace is bounded: after LOOP_FRAMES frames the program spins in
# `while True: pass`, the same way compat-cp-life ends, so CPython, the emulated
# Uno and real CircuitPython all emit the same transaction stream.
import board
import adafruit_ssd1306

LOOP_FRAMES = 4

i2c = board.I2C()
display = adafruit_ssd1306.SSD1306_I2C(128, 32, i2c)

frames = 0
while True:
    display.text("PyMCU", 0, 0, 1)
    display.show()
    frames = frames + 1
    if frames == LOOP_FRAMES:
        while True:
            pass
