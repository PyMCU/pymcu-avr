# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# The IndexError is catchable like any other, bound or not, and a handler for another type
# does not catch it.
from pymcu.types import uint8

xs: uint8[2] = [1, 2]


def get(i: int) -> int:
    return xs[i]


try:
    get(5)
except IndexError:
    print("caught")
try:
    try:
        get(5)
    except ValueError as e:
        print("wrong", e)
except IndexError:
    print("outer")
try:
    get(-3)
except Exception as e:
    print(e)
print("END")
