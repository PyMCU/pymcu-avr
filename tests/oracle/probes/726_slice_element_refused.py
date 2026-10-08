# expect: refuse a number
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Review round 6, finding 6 (nested-slice shape): slicing a buffer then subscripting
# the slice (`buf[0:1][0]`) is still one scalar element -- the OUTER subscript's
# target is itself a slice-of-a-buffer, not a bare name, which the matcher did not
# recurse into. CPython raises TypeError.
from pymcu.types import uint8


def head(v: bytearray) -> uint8:
    return v[0]


def main():
    buf = bytearray([10, 20])
    result: uint8 = head(buf[0:1][0])
    print(result)


main()
print("END")
