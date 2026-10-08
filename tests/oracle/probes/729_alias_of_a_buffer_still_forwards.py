# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Control for review round 6: a local alias of a buffer must keep forwarding as the
# buffer itself, never as a false positive from the scalar-element hardening above.
from pymcu.types import uint8


def head(v: bytearray) -> uint8:
    return v[0]


def main():
    buf = bytearray([10, 20])
    alias = buf
    result: uint8 = head(alias)
    print(result)


main()
print("END")
