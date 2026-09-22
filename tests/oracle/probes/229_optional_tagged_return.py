# expect: match
# doc: docs/language/roadmap.md
try:
    from typing import Optional
except ImportError:
    pass
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8
from pymcu.hal.console import print


def read(k: uint8) -> Optional[uint8]:
    if k == 0:
        return None
    return k + 7


a = read(GPIOR0.value)        # None (GPIOR0 reads 0 on both engines)
b = read(GPIOR0.value + 5)    # 12
if a is None:
    print("a-none")
else:
    print(a)
if b is not None:
    print(b)
else:
    print("b-none")
print(a or 1)
print(b or 1)
print("END")
