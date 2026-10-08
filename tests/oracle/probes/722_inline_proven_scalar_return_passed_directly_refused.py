# expect: refuse a number
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Review round 6, finding 5: a function's return value carries no name of its own to
# check provenScalarElements against, so a call result was never recognised as a
# scalar element. `element` is @inline and always returns buf[0]. CPython raises
# TypeError.
from pymcu.types import uint8, inline


def head(v: bytearray) -> uint8:
    return v[0]


@inline
def element(buf: bytearray) -> uint8:
    return buf[0]


def main():
    buf = bytearray([10, 20])
    result: uint8 = head(element(buf))
    print(result)


main()
print("END")
