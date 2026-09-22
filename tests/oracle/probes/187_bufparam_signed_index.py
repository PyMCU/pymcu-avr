# expect: match
# doc: docs/language/roadmap.md
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8, int16

def poke(buf: bytearray, k: int16):
    buf[k] = 0x55

def poke_prod(buf: bytearray, a: uint8, b: uint8):
    k: int16 = a * b
    buf[k] = 0x66

data: uint8[600] = bytearray(600)
seed: int16 = GPIOR0.value
poke(data, seed + 256)
poke_prod(data, 16 + uint8(seed), 16)
print(data[256], data[0])
print("END")
