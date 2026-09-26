# expect: refuse :9:10: error: SyntaxError: Expected newline or end of block
# doc: docs/language/roadmap.md:65
# frontend: default
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

seed: uint8 = GPIOR0.value
v: uint8 = seed + 6
label = t"level {v}"
print(label.strings[0])
print("END")
