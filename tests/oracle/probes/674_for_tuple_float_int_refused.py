# expect: refuse would have to hold both
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# A float and an integer are different value representations, not just different
# widths of the same one -- CPython answers by letting the name hold a different type
# each iteration, which one static, single-typed slot cannot. Refused by name instead
# of silently truncating the float into whichever integer type the loop settled on.
from pymcu.types import uint8


def f(a: float, b: uint8) -> None:
    for x in (b, a):
        print(x)
        continue
    print(x)


f(1.5, 2)
print("END")
