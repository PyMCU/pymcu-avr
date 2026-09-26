# expect: refuse :10:10: error: SyntaxError: match pattern MatchMapping
# doc: https://github.com/PyMCU/PyMCU/issues/523
# frontend: py-parser
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

seed: uint8 = GPIOR0.value
subject: uint8 = seed + 3
match subject:
    case {"gain": g}:
        print(g)
    case _:
        print(subject)
print("END")
