# expect: refuse would have to hold both
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# int32 and uint32 together would need 33 bits to hold both exactly -- there is no type
# here that represents the full range of both, so joining them into one slot (the
# nearest-fit int32, losing uint32's top half) would be a silent wrong answer rather
# than a width any single Python-level type actually is. Refused by name instead, with
# both incompatible types in the message.
from pymcu.types import int32, uint32


def f(a: uint32, b: int32) -> None:
    for x in (a, b):
        print(x)
        break


f(0xFFFFFFFF, -1)
print("END")
