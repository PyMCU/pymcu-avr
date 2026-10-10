# busio.py's own shape: constants assigned under `if __CHIP__.arch == ...`,
# at MODULE level, then imported elsewhere under an alias
# (`from pymcu.hal.i2c import I2C_OK as _I2C_OK`). Two different values per
# arch so a comparison that silently read zero either way, or read one
# constant for the other, cannot pass by accident.
from pymcu.chips import __CHIP__

if __CHIP__.arch == "avr":
    OK_VAL = 10
    OTHER_VAL = 20
else:
    OK_VAL = 30
    OTHER_VAL = 40
