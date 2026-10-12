# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# The scan loop of adafruit_onewire written at module level: a name that is None, then bound to a
# buffer the call returns, then fed back. A module-level name is a global, so it is declared once
# (a scalar for the None and an array for the buffer were two definitions of one symbol).
from typing import Optional, Tuple


class Bus:
    def search(self, l_rom: Optional[bytearray], diff: int) -> Tuple[bytearray, int]:
        if diff == 99:
            return None, 0
        if not l_rom:
            l_rom = bytearray(8)
        rom = bytearray(8)
        for i in range(8):
            rom[i] = l_rom[i] + 1
        return rom, diff - 1


bus = Bus()
rom = None
diff = 3
for _ in range(3):
    rom, diff = bus.search(rom, diff)
    if rom:
        print(rom[0])
    print(diff)
print("END")
