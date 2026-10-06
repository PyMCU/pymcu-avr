# expect: match
# doc: https://docs.pymcu.org/roadmap/
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8, uint16, const

T8: const[uint8[300]] = [0]*256 + [11, 22, 33, 44] + [0]*40

seed: uint16 = GPIOR0.value
print(T8[seed + 257])
print(T8[seed + 259])
print("END")
