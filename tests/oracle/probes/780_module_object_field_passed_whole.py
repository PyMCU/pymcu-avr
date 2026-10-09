# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# A buffer field on a MODULE-level object, passed whole to a function expecting a
# bytearray. CPython reads box.buf[0]; before the fix, the field's own flattened
# storage name was never declared, and the linker failed with "undefined reference".
from pymcu.types import uint8


class Box:
    def __init__(self, buf):
        self.buf = buf


box = Box(bytearray([10, 20]))


def head(v: bytearray) -> uint8:
    return v[0]


def main():
    result: uint8 = head(box.buf)
    print(result)


main()
print("END")
