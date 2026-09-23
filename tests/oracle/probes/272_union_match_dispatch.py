# expect: match
# doc: docs/language/roadmap.md
# RFC 0009 phase 3: `match` on a tagged union compares the tag -- `case None:`,
# `case int():`, `case float():` name members; a literal compares the payload;
# inside an arm the subject narrows to that member.
from typing import Union
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print


def pick(k: int) -> Union[int, float, None]:
    if k == 0:
        return None
    if k == 1:
        return 2.5
    return k


r = pick(GPIOR0.value + 1)    # 2.5
match r:
    case None:
        print("none")
    case int():
        print("int", r)
    case float():
        print("float", r)
    case _:
        print("wild")

c = pick(GPIOR0.value + 7)    # 7
match c:
    case 7:
        print("seven")
    case int():
        print("int", c)
    case _:
        print("wild")

n = pick(GPIOR0.value)        # None
match n:
    case None:
        print("is-none")
    case _:
        print("wild")
print("END")
