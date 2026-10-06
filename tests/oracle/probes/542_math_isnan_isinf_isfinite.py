# expect: match
# doc: https://docs.pymcu.org/limitations/
# math.isnan/isinf/isfinite, previously absent. @inline, three bit operations each, reading
# the same exponent/mantissa split _f32_repr already used to print inf/nan correctly.
import math
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
nan_v: float = float('nan') + float(s)
inf_v: float = float('inf') + float(s)
finite_v: float = float(s) + 1.5

print(math.isnan(nan_v))
print(math.isnan(inf_v))
print(math.isnan(finite_v))
print(math.isinf(nan_v))
print(math.isinf(inf_v))
print(math.isinf(finite_v))
print(math.isfinite(nan_v))
print(math.isfinite(inf_v))
print(math.isfinite(finite_v))
print("END")
