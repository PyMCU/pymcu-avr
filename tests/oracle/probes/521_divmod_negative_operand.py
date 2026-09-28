# expect: match
# doc: docs/language/limitations.md:1049
# divmod() floors like // and % and its results keep the division's width and sign. The
# fold truncated toward zero and stored the pair as bytes (253, 254 for -17, 5), and with a
# run-time int16 dividend the unpack targets were uint8 (113 for -143). The `//` and `%`
# lines on the same operands are the control.
from pymcu.types import int16
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
q, r = divmod(-17, 5)
print(q)
print(r)
q, r = divmod(17, -5)
print(q)
print(r)
a: int16 = s - 1000
q2, r2 = divmod(a, s + 7)
print(q2)
print(r2)
q3, r3 = divmod(s + 1000, 7)
print(q3)
print(r3)
print(a // (s + 7))
print(a % (s + 7))
print("END")
