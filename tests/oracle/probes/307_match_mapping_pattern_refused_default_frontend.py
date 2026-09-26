# expect: refuse :8:1: error: CompileError: dict/set literals are compile-time lookup tables
# doc: https://github.com/PyMCU/PyMCU/issues/523
# frontend: default
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
