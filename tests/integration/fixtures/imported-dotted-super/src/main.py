# imported-dotted-super: super().__init__ on an imported dotted base.
#
# adafruit_ssd1306 writes
#   import adafruit_framebuf as framebuf
#   class _SSD1306(framebuf.FrameBuffer):
#       def __init__(...):
#           super().__init__(buffer, width, height, _FRAMEBUF_FORMAT)
#
# WHAT DISCRIMINATES: prints 10. A compile that still mangled
# framebuf_FrameBuffer would refuse super() as a missing builtin.
from pymcu.time import delay_ms
from pymcu.types import uint8
import adafruit_framebuf as framebuf


class OLED(framebuf.FrameBuffer):
    def __init__(self, width: uint8):
        super().__init__(width)


o = OLED(10)


def main():
    while True:
        print(o.width)
        print("END")
        delay_ms(1200)
