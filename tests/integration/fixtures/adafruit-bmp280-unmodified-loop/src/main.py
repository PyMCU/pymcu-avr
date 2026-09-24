# Loop twin of adafruit-bmp280-unmodified: the same unmodified driver and the
# same reads -- temperature, pressure, altitude through the Optional[float]
# properties -- but inside `while True:`, the shape flashed firmware actually
# has. A member access in a loop registers the receiver for the field-write
# walk: `bmp280.temperature` lowers to a method call that writes `self._t_fine`
# (None in __init__, an int after the first read -- a tagged union field), which
# is exactly what the module-level fixture cannot see.
# The wire trace is bounded: after LOOP_ITERS iterations the program spins in
# `while True: pass`, so CPython, the emulated Uno and real CircuitPython all
# emit the same transaction stream.
import board

import adafruit_bmp280

LOOP_ITERS = 2

i2c = board.I2C()
bmp280 = adafruit_bmp280.Adafruit_BMP280_I2C(i2c)
bmp280.sea_level_pressure = 1013.25

iters = 0
while True:
    temperature = bmp280.temperature
    pressure = bmp280.pressure
    if pressure is None:
        print("pressure disabled")
    else:
        altitude = bmp280.altitude
        print(temperature, pressure, altitude)
    iters = iters + 1
    if iters == LOOP_ITERS:
        while True:
            pass
