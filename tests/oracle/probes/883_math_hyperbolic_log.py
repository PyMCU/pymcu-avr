# expect: match
# doc: https://docs.pymcu.org/limitations/
# math.sinh / cosh / tanh / asinh / acosh / atanh and log2 / log10 / exp2, built from exp and
# log. Scaled to integers (x1000) for the float32-versus-float64 reason probe 278 gives.
import math

print(int(math.sinh(0.1) * 1000.0))
print(int(math.sinh(1.0) * 1000.0))
print(int(math.sinh(-2.0) * 1000.0))
print(int(math.cosh(0.0) * 1000.0))
print(int(math.cosh(1.5) * 1000.0))
print(int(math.tanh(0.2) * 1000.0))
print(int(math.tanh(-2.5) * 1000.0))
print(int(math.tanh(20.0) * 1000.0))
print(int(math.asinh(1.0) * 1000.0))
print(int(math.asinh(-3.0) * 1000.0))
print(int(math.acosh(2.0) * 1000.0))
print(int(math.atanh(0.5) * 1000.0))
print(int(math.log2(8.0) * 1000.0))
print(int(math.log2(10.0) * 1000.0))
print(int(math.log10(1000.0) * 1000.0))
print(int(math.log10(2.0) * 1000.0))
print(int(math.exp2(3.0) * 1000.0))
print(int(math.exp2(-1.5) * 1000.0))
print("END")
