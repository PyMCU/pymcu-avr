# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
"""A function that walks a bytearray parameter with `for b in data` sees the bytes of the buffer
each caller passes, whatever its length."""
from pymcu.types import uint8


def total(data: bytearray) -> uint8:
    s: uint8 = 0
    for b in data:
        s += b
    return s


short = bytearray(2)
short[0] = 3
short[1] = 4
longer = bytearray(5)
for i in range(5):
    longer[i] = i + 1
print(total(short))
print(total(longer))
print("END")
