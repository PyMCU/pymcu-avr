# expect: refuse operand out of range
# doc: docs/language/limitations.md -- const index >255 emits LDI out of range; pymcu-avr#32
from pymcu.types import uint8, uint16

def poke_far(buf: bytearray):
    buf[300] = 0x66
    buf[511] = 0x77

data: uint8[600] = bytearray(600)
poke_far(data)
print(data[300], data[511], data[0], data[44])
print("END")
