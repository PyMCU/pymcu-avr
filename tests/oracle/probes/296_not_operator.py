# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

def popcount(n: uint8) -> uint8:
    c: uint8 = 0
    while n != 0:
        c = c + (n & 1)
        n = n >> 1
    return c

seed: uint8 = GPIOR0.value
v: uint8 = seed + 6
print(not popcount(v))
print(not (v - 6))
print(not (v & 1))
if not (v & 1):
    print("even")
else:
    print("odd")
total: uint8 = 0
for i in range(6):
    if not (i & 1):
        total = total + i
print(total)
while not (v > 8):
    v = v + 1
print(v)
print("END")
