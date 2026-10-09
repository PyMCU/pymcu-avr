# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Same shape as 780, but the object is a LOCAL inside a function, not a module-level
# name -- a different qualification path for the field's flattened name.
from pymcu.types import uint8


class Box:
    def __init__(self, buf):
        self.buf = buf


def head(v: bytearray) -> uint8:
    return v[0]


def main():
    box = Box(bytearray([10, 20]))
    result: uint8 = head(box.buf)
    print(result)


main()
print("END")
