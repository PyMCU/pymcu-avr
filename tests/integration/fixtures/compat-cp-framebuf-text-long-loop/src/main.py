# Loop twin of compat-cp-framebuf-text-long: the same two long text() lines and
# show(), but inside `while True:` -- the shape flashed firmware actually has.
# A receiver method call in a loop walks the fields the method writes; the walk
# used to drop self._font's noneValued record and lose the BitmapFont binding.
# The wire trace is bounded: after LOOP_FRAMES frames the program spins in
# `while True: pass`, so CPython, the emulated Uno and real CircuitPython all
# emit the same transaction stream.
import board
import adafruit_ssd1306

LOOP_FRAMES = 4

i2c = board.I2C()
display = adafruit_ssd1306.SSD1306_I2C(128, 32, i2c)

frames = 0
while True:
    display.text("PyMCU hello world", 0, 0, 1)
    display.text("AVR says hello world!", 0, 10, 1)
    display.show()
    frames = frames + 1
    if frames == LOOP_FRAMES:
        while True:
            pass
