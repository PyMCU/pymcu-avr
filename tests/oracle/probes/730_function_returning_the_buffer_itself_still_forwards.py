# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Control for review round 6: a function that returns the BUFFER ITSELF (not one of
# its elements) must keep forwarding as a buffer -- functionsReturnProvenScalar only
# marks a function whose return is a proven SCALAR element, never a plain pass-through.
from pymcu.types import uint8


def head(v: bytearray) -> uint8:
    return v[0]


def identity(buf: bytearray) -> bytearray:
    return buf


def main():
    buf = bytearray([10, 20])
    result: uint8 = head(identity(buf))
    print(result)


main()
print("END")
