# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# The signed/unsigned sibling of 666: a: int8, b: uint16. Joining a signed and an
# unsigned type for the widest-width decision needs a signed type one step above the
# unsigned one (int32), not just the larger of the two byte counts, or a negative `a`
# read back wrong once it shares a slot with an unsigned, wider element.
from pymcu.types import int8, uint16


def f(a: int8, b: uint16) -> None:
    x = a
    for x in (b, a):
        break
    print(x)


f(-1, 300)
print("END")
