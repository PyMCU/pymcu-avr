# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# `for x in (b, a): break` with b: uint16, a: uint8 left x at uint8 (a's width, the LAST
# unrolled element) even though break fires on the FIRST iteration, when x holds b's full
# 16 bits. The loop variable's type after the loop has to be the widest of every
# element's own type and whatever the name held before the loop, not whichever element
# happened to unroll last -- the backend homes one slot per name at one width.
from pymcu.types import uint8, uint16


def f(a: uint8, b: uint16) -> None:
    x = a
    for x in (b, a):
        break
    print(x)


f(1, 300)
print("END")
