# const-for-tuple-field: for cmd in (SET_DISP, ternary, self.height - 1).
#
# adafruit_ssd1306 init_display walks that tuple. BindUnrolledElement
# used to accept only integer literals.
#
# WHAT DISCRIMINATES: prints 237. 0xAE + 0 + 63.
from pymcu.time import delay_ms
from pymcu.types import uint8

SET_DISP = const(0xAE)


class OLED:
    def __init__(self):
        self.height: uint8 = 64
        self.page: uint8 = 0
        self.acc: uint8 = 0
        for cmd in (
            SET_DISP,
            0x10 if self.page else 0x00,
            self.height - 1,
        ):
            self.acc = self.acc + cmd


o = OLED()


def main():
    while True:
        print(o.acc)
        print("END")
        delay_ms(1200)
