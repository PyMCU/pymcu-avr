# expect: match
# doc: https://docs.pymcu.org/roadmap/
# RFC 0009 phase 3 (6.1): no annotation -- the member list is inferred from the
# return statements themselves (int on one arm, float on another, None at the
# end), and the tag is the same byte a declared union would carry.
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print


def guess(k: int):
    if k == 0:
        return 5
    if k == 1:
        return 2.5
    return None


a = guess(GPIOR0.value)        # 5
b = guess(GPIOR0.value + 1)    # 2.5
c = guess(GPIOR0.value + 3)    # None

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
