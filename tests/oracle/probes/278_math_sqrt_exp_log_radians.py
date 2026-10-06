# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# math.sqrt / exp / log / radians -- the four the measured corpus demands.
# Each result is scaled to an integer before printing: PyMCU carries float32 and
# CPython float64, so the two agree to about seven digits and disagree in the
# printed tail. The scales keep every value inside int16 and far from an integer
# boundary, so a real error in the series shows and a last-bit difference cannot.
import math

print(int(math.sqrt(144.0)))
print(int(math.sqrt(2.0) * 10000.0))
print(int(math.sqrt(0.25) * 1000.0))
print(int(math.exp(1.0) * 10000.0))
print(int(math.exp(0.0)))
print(int(math.exp(-2.0) * 10000.0))
print(int(math.log(100.0) * 1000.0))
print(int(math.radians(180.0) * 10000.0))
print("END")
