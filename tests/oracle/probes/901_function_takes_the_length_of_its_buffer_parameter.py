# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
"""`len(data)` on a bytearray parameter is the length of the buffer each caller passes."""
from pymcu.types import uint8


def last(data: bytearray) -> uint8:
    return data[len(data) - 1]


def span(data: bytearray) -> uint8:
    n: uint8 = 0
    for i in range(len(data)):
        n += 1
    return n


a = bytearray(3)
a[2] = 9
b = bytearray(7)
b[6] = 4
print(last(a))
print(last(b))
print(span(a))
print(span(b))
print("END")
