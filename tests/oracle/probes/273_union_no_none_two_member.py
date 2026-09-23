# expect: match
# doc: docs/language/roadmap.md
# RFC 0009 phase 3: Union without None -- two value members still take a tag
# when which one a call returns is only a run-time fact.
from typing import Union
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print


def pick2(k: int) -> Union[int, float]:
    if k == 0:
        return 9
    return 1.5


a = pick2(GPIOR0.value)        # 9
b = pick2(GPIOR0.value + 1)    # 1.5

if isinstance(a, float):
    print(a)
else:
    print(a)

if isinstance(b, float):
    print(b)
else:
    print(b)
print("END")
