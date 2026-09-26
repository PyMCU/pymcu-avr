# expect: refuse :7:1: error: SyntaxError: TypeAlias is not supported
# doc: https://github.com/PyMCU/PyMCU/issues/524
# frontend: py-parser
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

type Level = uint8

seed: Level = GPIOR0.value
print(seed + 11)
print("END")
