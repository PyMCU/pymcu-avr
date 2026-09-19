# memoryview-slice-view: FrameBuffer.buf is a memoryview window.
from pymcu.types import uint8

MVLSB: uint8 = 0


class MVLSBFormat:
    def fill(framebuf, color: uint8) -> uint8:
        for i in range(len(framebuf.buf)):
            framebuf.buf[i] = color
        return color


class FrameBuffer:
    def __init__(self, buf, width: uint8, height: uint8, buf_format: uint8 = MVLSB):
        self.buf = buf
        self.width = width
        self.height = height
        if buf_format == MVLSB:
            self.format = MVLSBFormat()

    def fill(self, color: uint8) -> uint8:
        return self.format.fill(self, color)

    def buflen(self) -> uint8:
        return len(self.buf)
