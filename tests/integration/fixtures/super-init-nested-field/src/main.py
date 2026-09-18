# super-init-nested-field: self.format = Fmt() inside the imported
# FrameBuffer.__init__ if, reached through super() on
# class OLED(framebuf.FrameBuffer).
#
# adafruit_ssd1306 writes that after
#   import adafruit_framebuf as framebuf
#   class _SSD1306(framebuf.FrameBuffer):
#       def __init__(...):
#           super().__init__(buffer, width, height, _FRAMEBUF_FORMAT)
#
# WHAT DISCRIMINATES: prints 7. A compile that still treated the
# super-expanded base ctor as outside __init__ would refuse
# self.format as a missing field of OLED.
from pymcu.time import delay_ms
from pymcu.types import uint8
import adafruit_framebuf as framebuf


class OLED(framebuf.FrameBuffer):
    def __init__(self):
        super().__init__(0)


o = OLED()


def main():
    while True:
        print(o.format.n)
        print("END")
        delay_ms(1200)
