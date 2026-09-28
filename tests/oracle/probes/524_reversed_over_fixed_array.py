# expect: match
# doc: docs/language/limitations.md:1046
# reversed() over a fixed array or a bytearray reads the array backwards. It read flattened
# slots no store writes and yielded zeros, with literal and with run-time elements. The
# index loop is the control; the list literal form is probe 454.
from pymcu.types import uint8, int16
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
a: uint8[3] = [s + 30, s + 7, s + 70]
for i in range(2, -1, -1):
    print(a[i])
for v in reversed(a):
    print(v)
bb = bytearray(2)
bb[0] = s + 9
bb[1] = s + 4
for w in reversed(bb):
    print(w)
n: int16[2] = [s - 300, s + 700]
for m in reversed(n):
    print(m)
k: uint8[2] = [5, 6]
for v in reversed(k):
    print(v)
print("END")
