# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
"""An empty list is false and a list with an element is true: the truth of a sequence is its
length. `if xs:` tested the heap pointer instead, which is never null, so an empty list took the
true branch."""
from pymcu.types import uint8

empty: list[uint8] = list()
if empty:
    print("empty is true")
else:
    print("empty is false")

one: list[uint8] = list()
one.append(7)
if one:
    print("one is true")
else:
    print("one is false")

one.append(8)
if one:
    print("two is true")
else:
    print("two is false")
print("END")
