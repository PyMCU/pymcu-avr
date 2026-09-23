# Surface-compatibility probe for adafruit_tcs34725, part A: construction and
# the register-backed properties. Part B covers the color-read pipeline.
# Member order makes the I2C read script deterministic: both sides consume the
# same readscript.txt bytes in the same sequence.
import board
import busio
import adafruit_tcs34725
from adafruit_tcs34725 import TCS34725

print("= module")
print(adafruit_tcs34725.__version__)
print(adafruit_tcs34725.__repo__)

i2c = busio.I2C(board.SCL, board.SDA)

print("= construct")
t = TCS34725(i2c)
print(isinstance(t, TCS34725))

print("= active")
print(t.active)
t.active = True
print(t.active)
t.active = True
t.active = False
print(t.active)

print("= integration_time")
print(t.integration_time)
t.integration_time = 24.0
print(t.integration_time)
t.integration_time = 614.4
print(t.integration_time)
try:
    t.integration_time = 1.0
except ValueError as e:
    print("V:", e)
try:
    t.integration_time = 700.0
except ValueError as e:
    print("V:", e)

print("= gain")
print(t.gain)
t.gain = 60
print(t.gain)
try:
    t.gain = 99
except ValueError as e:
    print("V:", e)

print("= interrupt")
print(t.interrupt)
t.interrupt = False
try:
    t.interrupt = True
except ValueError as e:
    print("V:", e)

print("= cycles")
print(t.cycles)
t.cycles = 10
print(t.cycles)
t.cycles = -1
try:
    t.cycles = 99
except ValueError as e:
    print("V:", e)

print("= thresholds")
print(t.min_value)
t.min_value = 0x5678
print(t.max_value)
t.max_value = 0x9ABC

print("= glass_attenuation")
print(t.glass_attenuation)
t.glass_attenuation = 2.0
print(t.glass_attenuation)
try:
    t.glass_attenuation = 0.5
except ValueError as e:
    print("V:", e)
t.glass_attenuation = 1.0

print("= construct error")
try:
    TCS34725(i2c)
except RuntimeError as e:
    print("R:", e)

print("=DONE=")
