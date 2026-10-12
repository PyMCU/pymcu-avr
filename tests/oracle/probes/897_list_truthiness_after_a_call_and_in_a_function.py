# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
"""The same rule for a list handed to a function, and for one that has been emptied of nothing:
the length is read where the test runs."""
from pymcu.types import uint8


def describe(xs: list[uint8]) -> uint8:
    if xs:
        return 1
    return 0


empty: list[uint8] = list()
full: list[uint8] = list()
full.append(9)
print(describe(empty))
print(describe(full))
print("END")
