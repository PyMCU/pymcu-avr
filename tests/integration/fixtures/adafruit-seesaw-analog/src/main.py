# Analog-read probe for the unmodified Adafruit Seesaw driver: the path that
# stores a pinmap CLASS OBJECT in self.pin_mapping and reads its class-level
# tuples (analog_pins) through it -- `pin not in self.pin_mapping.analog_pins`
# and `self.pin_mapping.analog_pins.index(pin)` inside analog_read.
import time

import board

from adafruit_seesaw.seesaw import Seesaw

i2c_bus = board.I2C()

while True:
    try:
        ss = Seesaw(i2c_bus)
        print(ss.analog_read(2))
    except Exception as e:
        print("no ack:", e)
    time.sleep(0.5)
