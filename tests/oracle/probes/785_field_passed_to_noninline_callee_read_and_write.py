# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# The field is passed to a real (non-inline) function that WRITES through the
# parameter, not just reads it -- the alias must be the real storage both ways, the
# same bytes box.buf itself reads back afterward.
from pymcu.types import uint8


class Box:
    def __init__(self, buf):
        self.buf = buf


def setfirst(v: bytearray, val: uint8):
    v[0] = val


def main():
    box = Box(bytearray([10, 20]))
    setfirst(box.buf, 99)
    print(box.buf[0])


main()
print("END")
