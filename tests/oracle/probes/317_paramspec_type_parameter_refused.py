# expect: refuse :6:10: error: SyntaxError: PEP 695 type parameters are not supported
# doc: https://github.com/PyMCU/PyMCU/issues/521
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

def twice[**P](n: uint8) -> uint8:
    return n + n

seed: uint8 = GPIOR0.value
print(twice(seed + 5))
print("END")
