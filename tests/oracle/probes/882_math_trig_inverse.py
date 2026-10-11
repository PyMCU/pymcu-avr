# expect: match
# doc: https://docs.pymcu.org/limitations/
# math.sin / cos / tan / atan / atan2 / asin / acos, float32 (Cephes' single-precision kernels).
# PyMCU carries float32 and CPython float64, so each result is scaled to an integer before
# printing: the two agree to about seven digits and disagree in the printed tail. The scale
# (x1000) keeps a real error in the polynomial or in the octant logic visible and a last-bit
# difference invisible. The arguments cover every octant, both signs, and a multi-turn angle.
import math

print(int(math.sin(0.5) * 1000.0))
print(int(math.sin(2.0) * 1000.0))
print(int(math.sin(-1.0) * 1000.0))
print(int(math.sin(10.0) * 1000.0))
print(int(math.cos(0.5) * 1000.0))
print(int(math.cos(2.5) * 1000.0))
print(int(math.cos(-3.0) * 1000.0))
print(int(math.cos(10.0) * 1000.0))
print(int(math.tan(0.5) * 1000.0))
print(int(math.tan(1.0) * 1000.0))
print(int(math.tan(-1.2) * 1000.0))
print(int(math.tan(2.0) * 1000.0))
print(int(math.atan(0.3) * 1000.0))
print(int(math.atan(1.0) * 1000.0))
print(int(math.atan(-2.0) * 1000.0))
print(int(math.atan(50.0) * 1000.0))
print(int(math.atan2(1.0, 1.0) * 1000.0))
print(int(math.atan2(1.0, -1.0) * 1000.0))
print(int(math.atan2(-1.0, -1.0) * 1000.0))
print(int(math.atan2(-1.0, 1.0) * 1000.0))
print(int(math.atan2(1.0, 0.0) * 1000.0))
print(int(math.asin(0.5) * 1000.0))
print(int(math.asin(-1.0) * 1000.0))
print(int(math.acos(0.3) * 1000.0))
print(int(math.acos(-0.7) * 1000.0))
print("END")
