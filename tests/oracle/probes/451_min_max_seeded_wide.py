# expect: match
# doc: https://docs.pymcu.org/limitations/#built-ins-summary
# min() and max() on run-time values that do not fit a byte, signed and unsigned mixed,
# as scalars, over a list, over fixed arrays, over a bytearray, and with key=. Probe 121
# passes literals. 300 and 299 differ only above the low byte, so a comparison done on
# eight bits picks the wrong one (44 against 43), and -5 against 0 catches an unsigned
# compare.
from pymcu.types import int16, uint8
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
x: int16 = s + 300
y: int16 = s + 299
z: int16 = s - 5


def neg(v: int16) -> int16:
    return 0 - v


print(max(x, y))
print(max(y, x))
print(max(x, y, z))
print(min(x, y, z))
print(min(y, x))
print(max(s + 3, x))
print(min(s + 3, z))
print(max(z, s))
xs = [y, x, z]
print(max(xs))
print(min(xs))
a: int16[3] = [s + 299, s + 300, s - 5]
print(max(a))
print(min(a))
u: uint8[3] = [s + 9, s + 200, s + 5]
print(max(u))
print(min(u))
bb = bytearray(3)
bb[0] = s + 9
bb[1] = s + 200
bb[2] = s + 5
print(max(bb))
print(min(bb))
print(max(x, s + 7, key=neg))
print("END")
