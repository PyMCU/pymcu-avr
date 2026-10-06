# expect: match
# doc: https://docs.pymcu.org/roadmap/
# RFC 0009 phase 3: a declared Union[int, float, None] return carries a member
# tag + widest-member payload; readers narrow before the payload is read.
from typing import Union
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print


def pick(k: int) -> Union[int, float, None]:
    if k == 0:
        return None
    if k == 1:
        return 2.5
    return k


a = pick(GPIOR0.value)        # k = 0 -> None
b = pick(GPIOR0.value + 1)    # k = 1 -> 2.5
c = pick(GPIOR0.value + 7)    # k = 7 -> 7

if isinstance(a, int):
    print(a)
elif isinstance(a, float):
    print(a)
else:
    print("a-none")

if isinstance(b, int):
    print(b)
elif isinstance(b, float):
    print(b)
else:
    print("b-none")

if isinstance(c, int):
    print(c)
elif isinstance(c, float):
    print(c)
else:
    print("c-none")
print("END")
