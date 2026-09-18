# descriptor-setter-augassign: Adafruit RWBits.__set__ shape.
#
# d.bits = 3 rewrites to Field.__set__(d, 3). The setter then does
#   value <<= self.shift
#   obj.reg |= value
# Binding value as a compile-time constant used to drop the name at the
# shift, so the next read said value was not defined.
#
# WHAT DISCRIMINATES: 48 (3 << 4). A compile that still lost value would
# not build.
from pymcu.types import uint8
from pymcu.time import delay_ms


class Field:
    def __init__(self, shift: uint8) -> None:
        self.shift = shift
        self.mask: uint8 = 0xF0

    def __set__(self, obj, value: uint8) -> None:
        value <<= self.shift
        obj.reg &= ~self.mask
        obj.reg |= value


class Dev:
    bits = Field(4)

    def __init__(self) -> None:
        self.reg: uint8 = 0


d = Dev()


def main():
    while True:
        d.bits = 3
        print(d.reg)
        print("END")
        delay_ms(1200)
