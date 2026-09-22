# expect: match
# doc: docs/language/roadmap.md
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8, uint16

data: uint8[600] = bytearray(600)
seed: uint16 = GPIOR0.value
data[seed + 255] = 1
data[seed + 256] = 2
data[seed + 511] = 3
data[seed + 512] = 4
print(data[255], data[256], data[511], data[512], data[0])
print("END")
