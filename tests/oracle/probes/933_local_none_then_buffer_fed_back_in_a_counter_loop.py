# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# The same scan loop inside a function with a local that starts as None, 30 turns.
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


def main():
    bus = Bus()
    rom = None
    diff: int = 3
    for _ in range(30):
        rom, diff = bus.search(rom, diff)
        if rom:
            print(rom[0])
        print(diff)
    print("END")


main()
