# expect: refuse unary '+' is not supported; it has no effect on a number, so write the operand on its own
# doc: docs/language/roadmap.md:16
# frontend: default
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

seed: uint8 = GPIOR0.value
v: uint8 = seed + 5
print(+v)
print("END")
