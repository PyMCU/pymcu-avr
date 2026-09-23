# Surface-compatibility probe for adafruit_tcs34725, part B: the color-read
# pipeline (color_raw, color_rgb_bytes, color, lux, color_temperature).
# Part A covers construction and the register-backed properties.
import board
import busio
from adafruit_tcs34725 import TCS34725

i2c = busio.I2C(board.SCL, board.SDA)
t = TCS34725(i2c)

print("= color_raw")
print(t.color_raw)

print("= color_rgb_bytes")
print(t.color_rgb_bytes)

print("= color")
print(t.color)

print("= lux")
print(t.lux)

print("= color_temperature")
print(t.color_temperature)

print("=DONE=")
