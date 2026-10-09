# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# The field is passed to an @inline function -- the field-aliasing fix must hold
# regardless of whether the callee is inlined or a real call.
from pymcu.types import uint8, inline


class Box:
    def __init__(self, buf):
        self.buf = buf


@inline
def head(v: bytearray) -> uint8:
    return v[0]


def main():
    box = Box(bytearray([10, 20]))
    result: uint8 = head(box.buf)
    print(result)


main()
print("END")
