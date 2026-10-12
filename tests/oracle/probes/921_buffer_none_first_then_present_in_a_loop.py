# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# A buffer that is None on the first turn of a loop and a buffer on every turn after: the result of
# a call is fed back as the next call's Optional[bytearray] argument (adafruit_onewire's scan loop).
# `if rom:` has to ask the presence the call left, not a None the name held before it.
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
        if diff > 1:
            return rom, diff - 1
        return rom, 0

    def scan(self) -> int:
        rom = None
        diff = 3
        for _ in range(6):
            rom, diff = self.search(rom, diff)
            print(diff)
            if rom:
                print(rom[0])
            if diff == 0:
                break
        return 0


b = Bus()
print(b.scan())
print("END")
