# expect: match
# doc: https://docs.pymcu.org/roadmap/
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8, uint16

def poke(buf: bytearray, k: uint16, v: uint8):
    buf[k] = v

def peek(buf: bytearray, k: uint16) -> uint8:
    return buf[k]

data: uint8[600] = bytearray(600)
seed: uint16 = GPIOR0.value
poke(data, seed + 255, 1)
poke(data, seed + 256, 2)
poke(data, seed + 511, 3)
poke(data, seed + 512, 4)
print(peek(data, seed + 255), data[255])
print(peek(data, seed + 256), data[256])
print(peek(data, seed + 511), data[511])
print(peek(data, seed + 512), data[512])
print(data[0])
print("END")
