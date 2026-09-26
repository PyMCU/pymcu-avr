# expect: match
# doc: https://github.com/PyMCU/PyMCU/issues/521
# frontend: py-parser
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

def twice[*Ts](n: uint8) -> uint8:
    return n + n

seed: uint8 = GPIOR0.value
print(twice(seed + 5))
print("END")
