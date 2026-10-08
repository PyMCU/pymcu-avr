# expect: refuse one element
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Review round 6, finding 4: a tuple-unpack target bound directly from a subscript
# (`one, ignored = (buf[0], 0)`) copied the scalar without propagating the proof --
# only a target bound from a NAME already proven that way did. CPython raises
# TypeError.
from pymcu.types import uint8


def head(v: bytearray) -> uint8:
    return v[0]


def main():
    buf = bytearray([10, 20])
    one, ignored = (buf[0], 0)
    result: uint8 = head(one)
    print(result)


main()
print("END")
