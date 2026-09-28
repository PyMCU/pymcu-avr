# expect: match
# doc: docs/language/limitations.md:1043
# sum() over a fixed array or a bytearray reads the array. It read flattened slots no store
# writes and printed 0, or whatever the previous expression left behind (the shape of
# #81), and added at the element width, so 200 + 100 + 50 wrapped. The explicit additions
# are the control and come LAST: placed before, they made sum(a) look right.
from pymcu.types import uint8, int16
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
a: uint8[3] = [s + 20, s + 10, s + 5]
print(sum(a))
k: uint8[3] = [20, 10, 5]
print(sum(k))
bb = bytearray(3)
bb[0] = s + 200
bb[1] = s + 100
bb[2] = s + 50
print(sum(bb))
w: int16[3] = [s - 300, s + 7, s - 2]
print(sum(w))
print(a[0] + a[1] + a[2])
print("END")
