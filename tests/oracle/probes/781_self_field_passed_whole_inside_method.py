# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# A method passes ITS OWN field (self.buf) whole to another function.
from pymcu.types import uint8


def head(v: bytearray) -> uint8:
    return v[0]


class Box:
    def __init__(self, buf):
        self.buf = buf

    def first(self) -> uint8:
        return head(self.buf)


def main():
    box = Box(bytearray([10, 20]))
    result: uint8 = box.first()
    print(result)


main()
print("END")
