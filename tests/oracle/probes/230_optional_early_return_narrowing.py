# expect: match
# doc: https://docs.pymcu.org/roadmap/
try:
    from typing import Optional
except ImportError:
    pass
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8
from pymcu.hal.console import print


def grab(k: uint8) -> Optional[uint8]:
    if k == 0:
        return None
    return k


def bump(k: uint8) -> Optional[uint8]:
    v = grab(k)
    if v is None:
        return None
    return v + 1


r1 = bump(GPIOR0.value + 4)   # grab(4) = 4, bump = 5
r2 = bump(GPIOR0.value)       # grab(0) = None, bump = None
print(r1 or 0)
print(r2 or 0)
if r1 is not None:
    print(r1)
print("END")
