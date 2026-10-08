# expect: refuse one element
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Review round 6, finding 3: a PLAIN `for` loop variable over a real buffer (not an
# enumerate()) was never recorded as a proven scalar element -- only the enumerate
# path did that. `one` is one byte of buf, read back as if it were the buffer's own
# address. CPython raises TypeError on the first iteration.
from pymcu.types import uint8


def head(v: bytearray) -> uint8:
    return v[0]


def main():
    buf = bytearray([10, 20])
    for one in buf:
        result: uint8 = head(one)
        print(result)


main()
print("END")
