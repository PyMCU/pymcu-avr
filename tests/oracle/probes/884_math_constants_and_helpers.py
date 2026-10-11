# expect: match
# doc: https://docs.pymcu.org/limitations/
# math.pi / e / tau / inf / nan (const[float]: folded, no storage), fabs, copysign, fmod, hypot,
# degrees, cbrt, expm1, log1p and the integer helpers gcd / lcm / isqrt / factorial.
import math
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
print(int(math.pi * 1000.0))
print(int(math.e * 1000.0))
print(int(math.tau * 1000.0))
print(math.isinf(math.inf))
print(math.isnan(math.nan))
print(math.isinf(math.nan))
print(int(math.fabs(-4.5) * 10.0))
print(int(math.copysign(3.0, -1.0) * 10.0))
print(int(math.copysign(-3.0, 1.0) * 10.0))
print(int(math.fmod(7.5, 2.0) * 10.0))
print(int(math.fmod(-7.5, 2.0) * 10.0))
print(int(math.hypot(3.0, 4.0) * 10.0))
print(int(math.hypot(-5.0, 12.0) * 10.0))
print(int(math.degrees(1.0) * 100.0))
print(int(math.cbrt(27.0) * 1000.0))
print(int(math.cbrt(-8.0) * 1000.0))
print(int(math.expm1(0.01) * 100000.0))
print(int(math.expm1(1.5) * 1000.0))
print(int(math.log1p(0.01) * 100000.0))
print(int(math.log1p(3.0) * 1000.0))
print(math.gcd(12 + s, -18))
print(math.lcm(4 + s, 6))
print(math.isqrt(99 + s))
print(math.isqrt(100 + s))
print(math.factorial(5 + s))
print(math.factorial(12 + s))
print("END")
