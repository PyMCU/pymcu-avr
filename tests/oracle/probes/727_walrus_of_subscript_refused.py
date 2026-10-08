# expect: refuse a number
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Review round 6, finding 6 (walrus shape): `head(one := buf[0])` passes the WALRUS
# expression directly as the argument -- the two-shape matcher never recognised a
# WalrusExpr argument at all, even though a later separate read of `one` already
# worked. CPython raises TypeError.
from pymcu.types import uint8


def head(v: bytearray) -> uint8:
    return v[0]


def main():
    buf = bytearray([10, 20])
    one: uint8 = 0
    result: uint8 = head(one := buf[0])
    print(result)


main()
print("END")
