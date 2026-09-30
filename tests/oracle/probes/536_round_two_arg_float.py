# expect: match
# doc: docs/language/limitations.md
# round(x, n) on a float, n a compile-time constant -- previously a flat refusal. Half-to-even
# on the EXACT decimal expansion of the float32 value, like round(x, n) is on a float64 in
# CPython. GPIOR0 keeps x from folding away entirely.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
x: float = float(s) / 7.0 + 3.14159

print(round(x, 2))
print(round(3.14159, 2))
print(round(2.5, 0))
print(round(-2.675, 2))
print(round(1.005, 2))
print(round(100.0, -1))
print(round(0.125, 2))
print("END")
