# Long-string twin of compat-cp-framebuf-text: display.text() lines of 17 and
# 21 characters. text() iterates each line with
# `for i, char in enumerate(chunk)`, and a compile-time string past the 8-char
# unroll cap used to be refused at compile time. Now the string is interned in
# flash and the loop reads it at run time (ArrayLoadFlash/LPM): char is a
# run-time uint8 char code, ord(char) passes it through, and draw_char's
# `self._font.seek(2 + (ord(char) * self.font_width) + char_x)` computes a
# run-time offset into the embedded font blob. show() then writes the 128x32
# MONO_VLSB framebuffer over I2C; the test compares every transaction with
# what CPython emits running the same sources (oracle/).
import board
import adafruit_ssd1306

i2c = board.I2C()
display = adafruit_ssd1306.SSD1306_I2C(128, 32, i2c)

display.text("PyMCU hello world", 0, 0, 1)
display.text("AVR says hello world!", 0, 10, 1)
display.show()
