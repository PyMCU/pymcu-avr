# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# A parameter Optional[bytearray] rebound to a fresh buffer under a condition decided at run time
# (`if not l_rom: l_rom = bytearray(8)`), then read: it names the caller's buffer when there was
# one and the new zeroed one when there was not.
from typing import Optional, Tuple


class Bus:
    def search(self, l_rom: Optional[bytearray], diff: int) -> Tuple[bytearray, int]:
        if diff == 99:
            return None, 0
        if not l_rom:
            l_rom = bytearray(8)
        print(l_rom[0])
        rom = bytearray(8)
        for i in range(8):
            rom[i] = l_rom[i] + 1
        if diff > 1:
            return rom, diff - 1
        return rom, 0

    def scan(self) -> int:
        rom = None
        diff = 3
        for _ in range(6):
            rom, diff = self.search(rom, diff)
            if rom:
                print(rom[0])
            if diff == 0:
                break
        return 0


b = Bus()
print(b.scan())
print("END")
