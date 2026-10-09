# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# A buffer two field-hops deep (obj.inner.buf) passed whole -- the field-aliasing fix
# must chain through an intermediate object field, not just a direct self.field.
from pymcu.types import uint8


class Inner:
    def __init__(self, buf):
        self.buf = buf


class Outer:
    def __init__(self, buf):
        self.inner = Inner(buf)


def head(v: bytearray) -> uint8:
    return v[0]


def main():
    obj = Outer(bytearray([10, 20]))
    result: uint8 = head(obj.inner.buf)
    print(result)


main()
print("END")
