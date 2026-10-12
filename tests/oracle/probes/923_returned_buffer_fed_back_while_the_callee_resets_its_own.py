# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# The buffer a call returns is fed back to the same call, which makes its own bytearray(8) afresh and
# then reads the one it was given (adafruit_onewire's _search_rom). They are two objects in CPython,
# so the zeroing of the new one must not change what the old one holds. The `else` arm is part of
# the shape: it is what once made the next call see None.
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
            else:
                print(-1)
            if diff == 0:
                break
        return 0


b = Bus()
print(b.scan())
print("END")
