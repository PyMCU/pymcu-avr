# expect: match
# doc: LANGUAGE_ROADMAP.md:63
from pymcu.types import uint8


def search_rom(n: uint8):
    rom = bytearray(2)
    rom[0] = n
    rom[1] = 40
    return rom, 6


rom, diff = search_rom(30)
print(rom[1], diff)
print("END")
