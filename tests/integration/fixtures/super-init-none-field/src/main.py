# super-init-none-field: super().__init__(reset=reset) with reset
# defaulting to None, then if self.reset_pin: must fold.
#
# adafruit_ssd1306 writes that on _SSD1306; without the fold the
# then branch lowered Pin.low() on a port that was never a register.
#
# WHAT DISCRIMINATES: prints 1. A compile that still lowered the
# guarded use would store 99.
from pymcu.time import delay_ms
from pymcu.types import uint8


class Base:
    def __init__(self, reset=None):
        self.reset_pin = reset
        if self.reset_pin:
            self.ok: uint8 = 99
        else:
            self.ok: uint8 = 1


class OLED(Base):
    def __init__(self, reset=None):
        super().__init__(reset=reset)


o = OLED()


def main():
    while True:
        print(o.ok)
        print("END")
        delay_ms(1200)
