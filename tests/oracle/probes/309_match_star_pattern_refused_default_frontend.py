# expect: refuse :10:17: error: SyntaxError: Expected expression
# doc: https://github.com/PyMCU/PyMCU/issues/524
# frontend: default
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

seed: uint8 = GPIOR0.value
data = [seed + 7, seed + 8, seed + 9]
match data:
    case [head, *tail]:
        print(head)
    case _:
        print(0)
print("END")
