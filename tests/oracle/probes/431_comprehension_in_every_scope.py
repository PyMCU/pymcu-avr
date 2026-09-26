# expect: match
# doc: docs/language/limitations.md:708
# The corpus had 24 comprehensions at module level, one in a method and NONE in a function.
# This is the single-clause, unfiltered form (the supported one) in all five scopes,
# including an @inline method, which no probe had used for anything.
#
# `base` is seeded from GPIOR0 so the elements are not folded, and the three elements are
# distinct so a comprehension that computed all zeros would show as 0, 0, 0 rather than
# agreeing by accident on the first one.
from pymcu.types import inline, uint8
from pymcu.chips.atmega328p import GPIOR0

base = 3 + GPIOR0.value


def in_a_function():
    vals = [x * base + 1 for x in range(3)]
    print(vals[0])
    print(vals[2])


@inline
def in_an_inline_function():
    vals = [x * base + 2 for x in range(3)]
    print(vals[0])
    print(vals[2])


class C:
    def in_a_method(self):
        vals = [x * base + 3 for x in range(3)]
        print(vals[0])
        print(vals[2])

    @inline
    def in_an_inline_method(self):
        vals = [x * base + 4 for x in range(3)]
        print(vals[0])
        print(vals[2])


mod = [x * base + 0 for x in range(3)]
print(mod[0])
print(mod[2])
in_a_function()
in_an_inline_function()
c = C()
c.in_a_method()
c.in_an_inline_method()
print("END")
