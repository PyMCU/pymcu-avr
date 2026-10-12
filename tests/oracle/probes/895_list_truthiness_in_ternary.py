# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
"""`a if xs else b` picks by the length of the list."""
from pymcu.types import uint8

empty: list[uint8] = list()
full: list[uint8] = list()
full.append(3)
a: uint8 = 10 if empty else 20
b: uint8 = 10 if full else 20
print(a)
print(b)
print("END")
