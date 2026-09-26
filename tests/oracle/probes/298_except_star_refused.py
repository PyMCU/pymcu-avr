# expect: refuse 'except*' (exception groups) is not supported
# doc: docs/language/limitations.md:276
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

seed: uint8 = GPIOR0.value
try:
    if seed == 0:
        raise ValueError("boom")
    print(seed)
except* ValueError:
    print("caught")
print("END")
