# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# A run-time index past the end of a fixed array raises IndexError instead of reading the
# neighbouring memory; a negative one counts from the end, as in CPython.
from pymcu.types import uint8

xs: uint8[4] = [10, 20, 30, 40]


def get(i: int) -> int:
    return xs[i]


def put(i: int, v: int) -> None:
    xs[i] = v


print(get(0))
print(get(3))
print(get(-1))
print(get(-4))
try:
    print(get(4))
except IndexError as e:
    print(e)
try:
    print(get(-5))
except IndexError as e:
    print(e)
try:
    put(9, 1)
except IndexError as e:
    print(e)
put(-1, 99)
print(get(3))
print(xs[0], xs[1], xs[2], xs[3])
print("END")
