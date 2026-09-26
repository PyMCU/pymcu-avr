# expect: refuse 'async with' is not supported: it suspends, and the coroutine-to-state-machine lowering is not implemented yet
# doc: https://github.com/PyMCU/PyMCU/issues/524
# frontend: default
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
