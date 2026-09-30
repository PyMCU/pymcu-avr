# expect: match
# doc: docs/language/limitations.md
# Every comparison with a NaN is False in CPython except != (always True). The AVR backend
# used GCC's __cmpsf2 for every comparison, which answers "greater" (0x01) for an
# unordered (NaN) pair -- harmless for == != < <=, but > and >= read that as a genuine
# "greater than", so `nan > 1.0` answered True. __gtsf2/__gesf2 (libgcc's own routines for
# those two operators) answer "not greater"/"not greater-or-equal" for the unordered case
# instead, matching CPython.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
n: float = float('nan') + float(s)

print(n == n)
print(n != n)
print(n < 1.0)
print(n <= 1.0)
print(n > 1.0)
print(n >= 1.0)
print(1.0 < n)
print(1.0 > n)
print("END")
