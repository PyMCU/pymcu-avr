# expect: match
# doc: https://docs.pymcu.org/limitations/#built-ins-summary
# zip() and enumerate() over run-time elements wider than a byte, over lists and over
# fixed arrays. Probes 011 and 013 iterate literal lists. Each line combines both halves
# into one number, so a swapped, narrowed or repeated element prints a different value.
from pymcu.types import int16
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
xs = [s + 1, s + 2]
ys = [s + 300, s + 400]
for a, b in zip(xs, ys):
    print(a * 1000 + b)
p: int16[2] = [s + 300, s + 7]
q: int16[2] = [s + 1, s + 2]
for c, d in zip(p, q):
    print(c * 10 + d)
es = [s + 300, s + 5]
for i, v in enumerate(es):
    print(i * 1000 + v)
for j, w in enumerate(p):
    print(j * 1000 + w)
print("END")
