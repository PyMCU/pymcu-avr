# expect: match
# doc: docs/language/roadmap.md
# tracked: #32
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8, uint16

def bump(buf: bytearray, k: uint16):
    buf[k] += 1

def setbit(buf: bytearray, k: uint16, m: uint8):
    buf[k] |= m

data: uint8[512] = bytearray(512)
data[256] = 10
data[0] = 3
seed: uint16 = GPIOR0.value
bump(data, seed + 256)
setbit(data, seed + 256, 0x80)
print(data[256], data[0])
print("END")
