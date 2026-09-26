# expect: refuse :17:5: error: SyntaxError: AsyncWith is not supported
# doc: docs/language/limitations.md:902
# frontend: py-parser
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

class Gate:
    def __init__(self, step: uint8):
        self.step = step
    async def __aenter__(self):
        return self
    async def __aexit__(self, typ, val, tb):
        return False

async def run(step: uint8) -> uint8:
    s: uint8 = 0
    async with Gate(step) as g:
        s = s + g.step
    return s

seed: uint8 = GPIOR0.value
print(run(seed + 2))
print("END")
