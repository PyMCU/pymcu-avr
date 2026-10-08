# expect: refuse a number
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Review round 6, finding 6 (field shape): a field access subscript (`obj.buf[0]`) hid
# the scalar element from the two-shape matcher ArgumentIsScalarElement used to be.
# CPython raises TypeError.
from pymcu.types import uint8


class Box:
    def __init__(self, buf):
        self.buf = buf


def head(v: bytearray) -> uint8:
    return v[0]


def main():
    obj = Box(bytearray([10, 20]))
    result: uint8 = head(obj.buf[0])
    print(result)


main()
print("END")
