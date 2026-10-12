# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# A module-level name that is None, passed once to an Optional[bytearray] parameter whose guard
# `if not l_rom:` folds, then read: the fresh buffer replaces the None, and the read is not a
# TypeError.
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
rom, diff = bus.search(rom, diff)
print(rom[0])
rom, diff = bus.search(rom, diff)
print(rom[0])
print("END")
