# param-shadows-module-alias: set_pixel's first data param is named framebuf.
from pymcu.types import uint8


class FrameBuffer:
    def __init__(self, stride: uint8):
        self.stride: uint8 = stride


class Fmt:
    @staticmethod
    def set_pixel(framebuf, x: uint8):
        return framebuf.stride
