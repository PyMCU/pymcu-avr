# expect: match
# doc: docs/language/limitations.md:53
# A list[T] element lives at base + 2 + index * size, and `xs[-1]` used -1 as it was: the
# read, the store and `+=` addressed the list header and printed or wrote its bytes.
from pymcu.types import uint8, uint16, int32
from pymcu.chips.atmega328p import GPIOR0
g0 = GPIOR0.value
xs: list[uint8] = []
xs.append(g0 + 5)
xs.append(g0 + 6)
xs.append(g0 + 7)
ys: list[uint16] = []
ys.append(g0 + 1000)
ys.append(g0 + 2000)
xs[-1] += 10
ys[-2] = g0 + 3000
print(xs[-1], xs[-3], ys[-1], ys[-2], ys[0])
print("END")
