# RFC 0008 demandant: display.text("PyMCU", 0, 0, 1) on an unmodified
# adafruit_ssd1306 + adafruit_framebuf pair. text() reaches
# BitmapFont("font5x8.bin"), whose open() resolves at compile time to the
# embedded blob (1282 bytes, md5 221fa943a9d845f68ab2155c75d644fe); show()
# writes the 128x32 MONO_VLSB framebuffer over I2C. The test compares every
# transaction with what CPython emits running the same sources (oracle/).
import board
import adafruit_ssd1306

i2c = board.I2C()
display = adafruit_ssd1306.SSD1306_I2C(128, 32, i2c)

display.text("PyMCU", 0, 0, 1)
display.show()
