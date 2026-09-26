# expect: refuse :7:11: error: SyntaxError: Expected '(' after function name
# doc: https://github.com/PyMCU/PyMCU/issues/521
# frontend: default
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

def larger[T](a: T, b: T) -> T:
    if a > b:
        return a
    return b

seed: uint8 = GPIOR0.value
print(larger(seed + 4, seed + 9))
print("END")
