# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# The no-break sibling of 666: the loop runs to completion and ends at the LAST
# element (a: uint8), but every iteration in between still has to use the SAME
# consistent width (uint16, b's own) -- the backend homes one slot per name at one
# width for every write into it, not a different width per iteration.
from pymcu.types import uint8, uint16


def f(a: uint8, b: uint16) -> None:
    x = a
    for x in (b, a):
        pass
    print(x)


f(1, 300)
print("END")
