# expect: match
# doc: https://docs.pymcu.org/roadmap/#language -- buffer param indexed by a >255 const into a u16 array; pymcu-avr#32
from pymcu.types import uint8, uint16

def poke_far(buf: bytearray):
    buf[300] = 0x66
    buf[511] = 0x77

data: uint8[600] = bytearray(600)
poke_far(data)
print(data[300], data[511], data[0], data[44])
print("END")
