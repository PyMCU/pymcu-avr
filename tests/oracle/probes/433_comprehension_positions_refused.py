# expect: refuse only supported where it fills a fixed array
# doc: https://docs.pymcu.org/limitations/#iterators-and-comprehensions
# A comprehension is supported where it fills a fixed array whose length is a compile-time
# constant, and refused everywhere else. Measured identical at module scope and inside a
# function for three positions: as an instance field, as a return value and as a call
# argument. The fourth, as a `for`-in iterable, refuses with a different diagnostic about the
# iterable forms and is left to the existing iterable probes.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0

base = 3 + GPIOR0.value


def make():
    return [x * base for x in range(3)]


got = make()
print(got[0])
print("END")
