# expect: match
# doc: docs/language/roadmap.md
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8, uint16

def poke(buf: bytearray, k: uint16):
    buf[k] = 0x55

def peek(buf: bytearray, k: uint16) -> uint8:
    return buf[k]

data: uint8[512] = bytearray(512)
seed: uint16 = GPIOR0.value
poke(data, seed + 256)
print(peek(data, seed + 256), data[0])
print("END")
