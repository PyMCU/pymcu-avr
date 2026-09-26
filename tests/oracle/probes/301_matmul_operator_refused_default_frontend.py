# expect: refuse :10:9: error: SyntaxError: Expected ')'
# doc: https://github.com/PyMCU/PyMCU/issues/524
# frontend: default
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

seed: uint8 = GPIOR0.value
a: uint8 = seed + 3
b: uint8 = seed + 4
print(a @ b)
print("END")
