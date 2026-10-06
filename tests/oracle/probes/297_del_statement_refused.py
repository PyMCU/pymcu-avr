# expect: refuse 'del' is not supported: storage here is static
# doc: https://docs.pymcu.org/limitations/#classes-and-inheritance
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

seed: uint8 = GPIOR0.value
x: uint8 = seed + 3
print(x)
del x
print("END")
