# super-init-nested-field: FrameBuffer assigns self.format inside if.
#
# adafruit_framebuf writes
#   if buf_format == MVLSB:
#       self.format = MVLSBFormat()
from pymcu.types import uint8


class Fmt:
    def __init__(self):
        self.n: uint8 = 7


class FrameBuffer:
    def __init__(self, kind: uint8):
        if kind == 0:
            self.format = Fmt()
        else:
            self.format = Fmt()
