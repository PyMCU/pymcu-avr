# expect: refuse a number
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Review round 6, finding 6 (ternary shape): both arms of the conditional expression
# are scalar elements of the same buffer -- the matcher never recursed into a
# TernaryExpr argument at all. CPython raises TypeError.
from pymcu.types import uint8


def head(v: bytearray) -> uint8:
    return v[0]


def main():
    buf = bytearray([10, 20])
    flag: uint8 = 1
    result: uint8 = head(buf[0] if flag else buf[1])
    print(result)


main()
print("END")
