# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Control for review round 6: a SLICE of a buffer is itself a buffer (never a
# scalar), and must keep forwarding as one -- IndexTargetHoldsScalarElements's new
# slice-target recursion must not mistake the slice's own result for a scalar.
from pymcu.types import uint8


def head(v: bytearray) -> uint8:
    return v[0]


def main():
    buf = bytearray([10, 20])
    result: uint8 = head(buf[0:1])
    print(result)


main()
print("END")
