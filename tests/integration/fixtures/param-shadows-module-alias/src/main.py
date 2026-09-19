# param-shadows-module-alias: import adafruit_framebuf as framebuf
# then @staticmethod set_pixel(framebuf, ...) reads framebuf.stride.
#
# adafruit_ssd1306 writes that import; MVLSBFormat.set_pixel names
# the FrameBuffer parameter framebuf. A compile that still mangled
# adafruit_framebuf_stride would refuse the member.
#
# WHAT DISCRIMINATES: prints 7.
from pymcu.time import delay_ms
from pymcu.types import uint8
import adafruit_framebuf as framebuf

fmt = framebuf.Fmt()
fb = framebuf.FrameBuffer(7)


def main():
    while True:
        print(fmt.set_pixel(fb, 0))
        print("END")
        delay_ms(1200)
