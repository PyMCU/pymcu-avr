# examples/ssd1306_simpletest.py from the Adafruit bundle, verbatim except for
# display_height = 64 -- the only upstream line that changes -- and one added
# line, display.text("PyMCU", 0, 0, 1) before the final show(). The program's
# second show() sits at the end, so a single run covers init on the 128x64
# geometry (multiplex 0x3F, COM pins 0x12), fill, pixels and text, and every
# show() writes the 1024-byte MONO_VLSB framebuffer as one 1025-byte
# transaction. font5x8.bin rides along as an embedded file (pyproject files=),
# the same blob the compat-cp-framebuf-text fixture uses.
# SPDX-FileCopyrightText: Tony DiCola
# SPDX-License-Identifier: CC0-1.0

# Basic example of clearing and drawing pixels on a SSD1306 OLED display.
# This example and library is meant to work with Adafruit CircuitPython API.

import board

import adafruit_ssd1306

# Create the I2C bus interface.
i2c = board.I2C()  # uses board.SCL and board.SDA
# i2c = busio.I2C(board.GP1, board.GP0)    # Pi Pico RP2040

# Create the SSD1306 OLED class.
display_width = 128
display_height = 64
display = adafruit_ssd1306.SSD1306_I2C(display_width, display_height, i2c)
# You can change the I2C address with an addr parameter:
# display = adafruit_ssd1306.SSD1306_I2C(display_width, display_height, i2c, addr=0x31)

# fills display with black pixels clearing it
display.fill(0)
display.show()

# Set a pixel in the origin 0,0 position.
display.pixel(0, 0, 1)
# Set a pixel in the middle 64, 16 position.
display.pixel(64, 16, 1)
# Set a pixel in the opposite 127, 31 position.
display.pixel(127, 31, 1)
display.text("PyMCU", 0, 0, 1)
display.show()
