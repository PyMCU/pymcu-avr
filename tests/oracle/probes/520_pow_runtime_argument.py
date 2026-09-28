# expect: match
# doc: docs/language/limitations.md:1050
# pow() with a run-time integer argument is integer exponentiation, the same unroll `**`
# lowers. It was forwarded to the float routine with integer bit patterns and printed 1.0
# (1 through an int16 annotation) whichever argument was the run-time one. `b ** 5` on the
# same name is the PyMCU-against-PyMCU control. A run-time exponent is refused, as for `**`.
from pymcu.types import int16
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
b = s + 3
print(pow(b, 5))
print(pow(s + 2, 10))
print(pow(s - 5, 3))
r: int16 = pow(b, 5)
print(r)
print(b ** 5)
print("END")
