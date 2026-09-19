# format-if-folds: FrameBuffer.__init__ picks A when fmt is MVLSB.
# Unused B.rect forwards framebuf to set_pixel; that body must not
# compile as an outlined subroutine (numeric stride).
from pymcu.types import uint8

MVLSB: uint8 = 0
OTHER: uint8 = 5


class A:
    def __init__(self):
        self.n: uint8 = 1

    @inline
    def get(self) -> uint8:
        return self.n

    @inline
    def fill(framebuf, color: uint8) -> uint8:
        return color

    def set_pixel(framebuf, x: uint8, y: uint8, color: uint8) -> uint8:
        return framebuf.stride

    def rect(framebuf, x: uint8, y: uint8, w: uint8, h: uint8, color: uint8) -> uint8:
        return A.set_pixel(framebuf, x, y, color)


class B:
    def __init__(self):
        self.n: uint8 = 2

    @inline
    def get(self) -> uint8:
        return self.n

    def fill(framebuf, color: uint8) -> uint8:
        framebuf.buf = [color for i in range(len(framebuf.buf))]
        return color

    def set_pixel(framebuf, x: uint8, y: uint8, color: uint8) -> uint8:
        return framebuf.stride

    def rect(framebuf, x: uint8, y: uint8, w: uint8, h: uint8, color: uint8) -> uint8:
        return B.set_pixel(framebuf, x, y, color)


class FrameBuffer:
    def __init__(self, buf_format: uint8 = MVLSB):
        self.buf: uint8[8] = [0, 0, 0, 0, 0, 0, 0, 0]
        self.stride: uint8 = 8
        if buf_format == MVLSB:
            self.format = A()
        elif buf_format == OTHER:
            self.format = B()
        else:
            self.format = B()

    def pick(self) -> uint8:
        return self.format.get()

    def fill(self, color: uint8) -> uint8:
        return self.format.fill(self, color)
