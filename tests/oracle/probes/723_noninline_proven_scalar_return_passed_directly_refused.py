# expect: refuse a number
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Review round 6, finding 5 (non-inline variant): the same gap with `element` compiled
# as a real, non-inline function. CPython raises TypeError.
from pymcu.types import uint8


def head(v: bytearray) -> uint8:
    return v[0]


def element(buf: bytearray) -> uint8:
    return buf[0]


def main():
    buf = bytearray([10, 20])
    result: uint8 = head(element(buf))
    print(result)


main()
print("END")
