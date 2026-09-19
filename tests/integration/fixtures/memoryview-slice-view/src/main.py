# memoryview-slice-view: memoryview(self.buffer)[1:] through
# I2C -> _SSD1306(buffer) -> FrameBuffer(buffer).
# MVLSBFormat.fill uses len(framebuf.buf) / framebuf.buf[i] = color.
# adafruit_ssd1306 keeps byte 0 as the I2C command that way.
#
# WHAT DISCRIMINATES: prints 3, 64, 7, 7, 7.
from pymcu.time import delay_ms
from pymcu.types import uint8
import adafruit_framebuf as framebuf

_FRAMEBUF_FORMAT = framebuf.MVLSB


class _SSD1306(framebuf.FrameBuffer):
    def __init__(self, buffer, width: uint8, height: uint8):
        super().__init__(buffer, width, height, _FRAMEBUF_FORMAT)


class OLED(_SSD1306):
    def __init__(self):
        self.buffer = bytearray([64, 1, 2, 3])
        super().__init__(memoryview(self.buffer)[1:], 3, 8)

    def byte0(self) -> uint8:
        return self.buffer[0]

    def byte1(self) -> uint8:
        return self.buffer[1]

    def byte2(self) -> uint8:
        return self.buffer[2]

    def byte3(self) -> uint8:
        return self.buffer[3]


fb = OLED()


def main():
    while True:
        print(fb.buflen())
        fb.fill(7)
        print(fb.byte0())
        print(fb.byte1())
        print(fb.byte2())
        print(fb.byte3())
        print("END")
        delay_ms(1200)
