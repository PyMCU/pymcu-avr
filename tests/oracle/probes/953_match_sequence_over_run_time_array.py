# expect: match
# doc: https://github.com/PyMCU/PyMCU/issues/401
# A sequence pattern over an array whose elements are only known at run time: the captures and
# the literals in the pattern read the array's own storage, not a slot named after it that
# nothing wrote (every capture was 0, every literal compared against 0).
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

seed: uint8 = GPIOR0.value
data = [seed + 7, seed + 8, seed + 9]
match data:
    case [head, mid, last]:
        print(head)
        print(mid)
        print(last)
    case _:
        print(0)

match data:
    case [7, y, z]:
        print(y + z)
    case _:
        print(0)

match data:
    case [8, y, z]:
        print(y + z)
    case [a, 8, c]:
        print(a * 10 + c)
    case _:
        print(0)


def classify(a: uint8, b: uint8, c: uint8) -> uint8:
    local = [a, b, c]
    match local:
        case [1, y, z]:
            return y + z
        case [x, 2, z]:
            return x + z
        case _:
            return 0


print(classify(seed + 1, seed + 5, seed + 6))
print(classify(seed + 4, seed + 2, seed + 6))
print(classify(seed + 4, seed + 5, seed + 6))
print("END")
