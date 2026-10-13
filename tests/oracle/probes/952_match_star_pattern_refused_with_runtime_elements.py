# expect: refuse :12:26: error: SyntaxError: a star pattern (`*rest`) in a sequence pattern is not supported
# doc: https://github.com/PyMCU/PyMCU/issues/401
# The Python front end used to read `*tail` as one more capture, so the arm was taken for a
# sequence of any length and the head came out 0; the C# parser stops at the star. Both refuse
# it, at the star.
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

seed: uint8 = GPIOR0.value
data = [seed + 1, seed + 2]
match data:
    case [first, second, *rest]:
        print(first)
    case _:
        print(0)
print("END")
