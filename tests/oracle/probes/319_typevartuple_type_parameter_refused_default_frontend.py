# expect: refuse :7:10: error: SyntaxError: Expected '(' after function name
# doc: docs/language/type-system.md
# frontend: default
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

def twice[*Ts](n: uint8) -> uint8:
    return n + n

seed: uint8 = GPIOR0.value
print(twice(seed + 5))
print("END")
