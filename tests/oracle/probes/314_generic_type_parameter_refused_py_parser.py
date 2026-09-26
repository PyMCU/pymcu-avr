# expect: refuse :7:1: error: CompileError: unknown type 'T' in the annotation
# doc: docs/language/type-system.md
# frontend: py-parser
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

def larger[T](a: T, b: T) -> T:
    if a > b:
        return a
    return b

seed: uint8 = GPIOR0.value
print(larger(seed + 4, seed + 9))
print("END")
