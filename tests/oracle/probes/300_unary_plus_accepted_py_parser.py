# expect: match
# doc: https://github.com/PyMCU/PyMCU/issues/524
# frontend: py-parser
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

seed: uint8 = GPIOR0.value
v: uint8 = seed + 5
print(+v)
print("END")
