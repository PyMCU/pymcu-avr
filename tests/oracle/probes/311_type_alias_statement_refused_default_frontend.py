# expect: refuse :7:6: error: SyntaxError: Expected newline or end of block
# doc: docs/language/type-system.md
# frontend: default
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

type Level = uint8

seed: Level = GPIOR0.value
print(seed + 11)
print("END")
