# format-if-folds: if buf_format == MVLSB keeps A(), not the last elif's B().
# _FRAMEBUF_FORMAT through super().__init__ must stay a constant.
# Unused B.rect -> set_pixel(framebuf) must not compile a numeric stride.
#
# WHAT DISCRIMINATES: prints 1 then 1.
from pymcu.time import delay_ms
from pymcu.types import uint8
import adafruit_framebuf as framebuf

_FRAMEBUF_FORMAT = framebuf.MVLSB


class OLED(framebuf.FrameBuffer):
    def __init__(self):
        super().__init__(_FRAMEBUF_FORMAT)


fb = OLED()


def main():
    while True:
        print(fb.pick())
        print(fb.fill(1))
        print("END")
        delay_ms(1200)
