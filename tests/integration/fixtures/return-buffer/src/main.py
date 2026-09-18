# PyMCU -- return-buffer: `data = f()` where f does `return <local bytearray>`
# must bind `data` to the callee's fixed slot -- the register-read shape every
# I2C/SPI driver writes (`_read_register` in adafruit_bmp280 and friends).
#
# The buffer is a static slot like every local, so the name is what travels
# back; `data[i]`, `data[i] = v`, `len(data)` and `for` all answer it.
#
# `base` comes from GPIOR0 (the test seeds 5) so the bytes are filled on the
# chip.
#
# Expected UART output, seed 5:
#   A 5 / B 7 / D 3 / E 99 / F 99 / F 6 / F 7
#   END
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8


class Drv:
    def _read(self, n: uint8):
        result = bytearray(n)
        i: uint8 = 0
        while i < n:
            result[i] = GPIOR0.value + i
            i = i + 1
        return result


def main():
    base: uint8 = GPIOR0.value
    d = Drv()
    data = d._read(3)
    print("A", data[0])
    print("B", data[2])
    print("D", len(data))
    data[0] = 99
    print("E", data[0])
    for b in data:
        print("F", b)
    print("END")


main()
