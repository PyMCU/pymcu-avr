# expect: match
# doc: docs/language/limitations.md:1048
# sum(genexp, start) over a fixed array of run-time bytes, the total past 255. The start
# argument is the documented half of the generator form. Every element is distinct, so
# a dropped or repeated one changes the total.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
a: uint8[3] = [s + 200, s + 100, s + 50]
print(sum((x for x in a), 1000))
print("END")
