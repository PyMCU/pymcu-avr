# expect: match
# doc: docs/language/roadmap.md:16
# frontend: py-parser
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

seed: uint8 = GPIOR0.value
v: uint8 = seed + 5
print(+v)
print("END")
