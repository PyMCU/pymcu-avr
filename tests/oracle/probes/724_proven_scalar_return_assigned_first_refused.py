# expect: refuse one element
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Review round 6, finding 5 (assigned-first variant): the call result is bound to a
# name before reaching head() -- the name must inherit the proof the call itself
# carries. CPython raises TypeError.
from pymcu.types import uint8


def head(v: bytearray) -> uint8:
    return v[0]


def element(buf: bytearray) -> uint8:
    return buf[0]


def main():
    buf = bytearray([10, 20])
    e: uint8 = element(buf)
    result: uint8 = head(e)
    print(result)


main()
print("END")
