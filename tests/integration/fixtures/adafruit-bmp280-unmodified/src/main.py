# Fixture program for the unmodified Adafruit BMP280 driver.
#
# The driver's pressure property is annotated `-> Optional[float]` upstream: the
# shape that motivated RFC 0009. This program calls temperature, pressure and
# altitude once each so the run terminates -- the oracle replays the same file
# under CPython and compares the I2C transaction stream, which prints below do
# not affect (they go to UART).
import board

import adafruit_bmp280

i2c = board.I2C()
bmp280 = adafruit_bmp280.Adafruit_BMP280_I2C(i2c)
bmp280.sea_level_pressure = 1013.25

temperature = bmp280.temperature
pressure = bmp280.pressure
if pressure is None:
    print("pressure disabled")
else:
    altitude = bmp280.altitude
    print(temperature, pressure, altitude)
print("END")
