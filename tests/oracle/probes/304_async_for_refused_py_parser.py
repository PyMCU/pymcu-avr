# expect: refuse :15:5: error: SyntaxError: AsyncFor is not supported
# doc: docs/language/limitations.md:902
# frontend: py-parser
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

async def counts(limit: uint8):
    i: uint8 = 0
    while i < limit:
        yield i
        i = i + 1

async def total(limit: uint8) -> uint8:
    s: uint8 = 0
    async for v in counts(limit):
        s = s + v
    return s

seed: uint8 = GPIOR0.value
print(total(seed + 4))
print("END")
