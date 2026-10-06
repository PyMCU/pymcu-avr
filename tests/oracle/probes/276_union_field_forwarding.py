# expect: match
# doc: https://docs.pymcu.org/roadmap/
# RFC 0009 phase 3: a union lives in an object field and forwards through a
# getter -- the field stores payload+tag and the reader narrows after the call.
from typing import Union
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print


def pick(k: int) -> Union[int, float, None]:
    if k == 0:
        return None
    if k == 1:
        return 2.5
    return k


class Box:
    def __init__(self) -> None:
        self.v: Union[int, float, None] = None
        self.n = 0

    def get(self) -> Union[int, float, None]:
        return self.v


b = Box()
b.v = GPIOR0.value + 5
r = b.get()
if isinstance(r, int):
    print(r)
elif isinstance(r, float):
    print(r)
else:
    print("none")

b.v = 2.5
r2 = b.get()
if isinstance(r2, float):
    print(r2)
elif isinstance(r2, int):
    print(r2)
else:
    print("none")

b.v = pick(GPIOR0.value)
r3 = b.get()
if r3 is None:
    print("r3-none")
elif isinstance(r3, int):
    print(r3)
elif isinstance(r3, float):
    print(r3)
else:
    print("r3-?")
print("END")
